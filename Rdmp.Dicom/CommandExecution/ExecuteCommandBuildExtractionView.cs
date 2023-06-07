using Rdmp.Core.CommandExecution;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Repositories.Construction;
using Rdmp.Core.Repositories.Managers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rdmp.Dicom.CommandExecution;

public class ExecuteCommandBuildExtractionView : BasicCommandExecution
{
    /// <summary>
    /// A top level table e.g. CT_StudyTable
    /// </summary>
    TableInfo primaryTable;
    /// <summary>
    /// <para>Tables that can be linked to with study instance UID e.g. CT_SeriesTable.</para>
    /// <para>Also includes any aggregate tables e.g. aggregate statistics captured at Series level</para>
    /// </summary>
    List<TableInfo> SeriesLevelTables = new List<TableInfo>();
    /// <summary>
    /// Tables that can be linked to with SeriesInstanceUID e.g. CT_ImageTable
    /// </summary>
    List<TableInfo> ImageLevelTables = new List<TableInfo>();

    public string CatalogueName { get; }

    [UseWithObjectConstructor]
    public ExecuteCommandBuildExtractionView(IBasicActivateItems activator,string catalogueName, TableInfo[] fromTables): base(activator)
    {
        if(activator.RepositoryLocator.CatalogueRepository.GetAllObjectsWhere<Catalogue>("Name",catalogueName).Any())
        {
            SetImpossible($"Catalogue(s) called {catalogueName} already exist");
            return;
        }

        try
        {
            ClassifyTables(fromTables);

            // multiple series level tables is allowed but not multi series and image level
            // ones as well or how do we know what order to join in! who do the image tables go to
            // when there are multiple series tables
            if (ImageLevelTables.Count > 0)
            {
                switch (SeriesLevelTables.Count)
                {
                    case > 1:
                        SetImpossible("Found multiple Series level tables and at least 1 Image level table");
                        return;
                    case 0:
                        SetImpossible("Found image level table(s) but no Series level tables");
                        return;
                }
            }

            EnsureAllHave("StudyInstanceUID", primaryTable);

            EnsureAllHave("StudyInstanceUID", SeriesLevelTables.ToArray());
            EnsureAllHave("SeriesInstanceUID", SeriesLevelTables.ToArray());

            EnsureAllHave("SeriesInstanceUID", ImageLevelTables.ToArray());
        }
        catch (Exception ex)
        {
            SetImpossible($"Could not classify tables:{ex.Message}");
        }
        CatalogueName = catalogueName;
    }

    private void EnsureAllHave(string col, params TableInfo[] tables)
    {
        foreach(var t in tables)
        {
            var match = GetColumnInfoCalled(t, col) ?? throw new Exception($"Expected to find a column called {col} in {t}");
        }
    }

    private ColumnInfo GetColumnInfoCalled(TableInfo t, string expectedColumnNamed)
    {
        return t.ColumnInfos.FirstOrDefault(c => c.GetRuntimeName().Equals(expectedColumnNamed));
    }

    private void ClassifyTables(TableInfo[] fromTables)
    {
        primaryTable = TryClassifyByName("StudyTable", fromTables);
        primaryTable ??= TryClassifyByPrimaryKey("StudyInstanceUID", fromTables);

        if (primaryTable == null)
            throw new Exception("Could not identify the Study level table");

        foreach(var tbl in fromTables.Except(new[] { primaryTable}))
        {
            if (IsSeriesTable(tbl))
            {
                SeriesLevelTables.Add(tbl);
            }
            else if(IsImageTable(tbl))
            {
                ImageLevelTables.Add(tbl);
            }
            else
            {
                throw new Exception($"Unknown table type {tbl.GetRuntimeName()}");
            }
        }
    }

    private bool IsImageTable(TableInfo tbl)
    {
        return (TryClassifyByName("ImageTable", tbl) ?? TryClassifyByPrimaryKey("SOPInstanceUID", tbl)) != null;
    }

    private bool IsSeriesTable(TableInfo tbl)
    {
        return (TryClassifyByName("SeriesTable", tbl) ?? TryClassifyByPrimaryKey("SeriesInstanceUID", tbl)) != null;
    }

    /// <summary>
    /// Identify study table because it has a primary key of StudyInstanceUID
    /// </summary>
    /// <param name="fromTables"></param>
    /// <returns></returns>
    private TableInfo TryClassifyByPrimaryKey(string name, params TableInfo[] fromTables)
    {
        var studyTables = fromTables.Where(t => HasPrimaryKey(t,name)).ToArray();

        return studyTables.Length == 1 ? studyTables[0] : null;
    }

    private bool HasPrimaryKey(TableInfo t, string name)
    {
        var pks = t.ColumnInfos.Where(c => c.IsPrimaryKey).ToArray();
        // multiple primary keys = false
        return pks.Length == 1 && pks[0].GetRuntimeName().Equals(name, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Identify study table based on its name 
    /// </summary>
    /// <param name="fromTables"></param>
    /// <returns></returns>
    private TableInfo TryClassifyByName(string name, params TableInfo[] fromTables)
    {
        var studyTables = fromTables.Where(t => t.GetRuntimeName().EndsWith(name, StringComparison.InvariantCultureIgnoreCase)).ToArray();

        return studyTables.Length == 1 ? studyTables[0] : null;
    }

    public override void Execute()
    {
        base.Execute();

        var joinManager = BasicActivator.RepositoryLocator.CatalogueRepository.JoinManager;

        primaryTable.IsPrimaryExtractionTable = true;
        primaryTable.SaveToDatabase();
                    
        foreach(var series in SeriesLevelTables)
        {
            SetupSubTableWithJoinsOf("StudyInstanceUID", primaryTable, series,joinManager);
        }

        foreach (var image in ImageLevelTables)
        {
            SetupSubTableWithJoinsOf("SeriesInstanceUID", SeriesLevelTables.Single(), image, joinManager);
        }

        var cata = new Catalogue(BasicActivator.RepositoryLocator.CatalogueRepository, CatalogueName);

        AddColumnsWhereNotExist(cata, primaryTable);
        AddColumnsWhereNotExist(cata, SeriesLevelTables.ToArray());
        AddColumnsWhereNotExist(cata, ImageLevelTables.ToArray());
    }

    private void AddColumnsWhereNotExist(Catalogue cata, params TableInfo[] tables)
    {
        foreach(var t in tables)
        {
            foreach(var col in t.ColumnInfos)
            {
                // don't cache any knowledge
                cata.ClearAllInjections();

                // we already know about this column
                if (cata.CatalogueItems.Any(ci => ci.Name.Equals(col.GetRuntimeName())))
                    continue;

                // add the new column
                var cmd = new ExecuteCommandAddNewCatalogueItem(BasicActivator, cata, col) { NoPublish = true ,Category = ExtractionCategory.Core};
                cmd.Execute();
            }
        }
    }

    private void SetupSubTableWithJoinsOf(string linkColumnName, TableInfo primaryTable, TableInfo subTable, IJoinManager joinManager)
    {
        // if we already know how to join these tables don't bother
        if (joinManager.GetAllJoinInfosBetweenColumnInfoSets(primaryTable.ColumnInfos, subTable.ColumnInfos).Any())
        {
            return;
        }

        // we aren't the head of this join network that's for sure
        subTable.IsPrimaryExtractionTable = false;
        subTable.SaveToDatabase();

        // create the join in the database;
        var j = new JoinInfo(BasicActivator.RepositoryLocator.CatalogueRepository,
            GetColumnInfoCalled(subTable, linkColumnName) ?? throw new Exception($"Could not find expected column {linkColumnName} in {subTable} to create join"),
            GetColumnInfoCalled(primaryTable, linkColumnName) ?? throw new Exception($"Could not find expected column {linkColumnName} in {primaryTable} to create join"),
            ExtractionJoinType.Right,
            null);
        j.SaveToDatabase();
    }
}