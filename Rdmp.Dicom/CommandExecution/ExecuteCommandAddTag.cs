using Rdmp.Core.CommandExecution;
using Rdmp.Core.Curation.Data;
using Rdmp.Dicom.TagPromotionSchema;
using ReusableLibraryCode.Checks;
using System;
using System.Linq;

namespace Rdmp.Dicom.CommandExecution
{
    /// <summary>
    /// Adds a new column to a <see cref="Catalogue"/> based on either a dicom tag (with infered datatype) or an explict name/datatype combination
    /// </summary>
    public class ExecuteCommandAddTag : BasicCommandExecution
    {
        private readonly TagColumnAdder _adder;

        public ExecuteCommandAddTag(BasicActivateItems activator,ICatalogue catalogue, 
            [DemandsInitialization("Name of the new column you want created.")]
            string column, 
            [DemandsInitialization("Optional when column is the name of a Dicom Tag e.g. StudyInstanceUID")]
            string dataType):base(activator)
        {
            var tables = catalogue.GetTableInfosIdeallyJustFromMainTables();

            if(tables.Length != 1)
            {
                SetImpossible($"There are {tables.Length} tables mapped under Catalogue {catalogue}");
                return;
            }

            if(string.IsNullOrWhiteSpace(column))
            {
                SetImpossible("Column name must be supplied");
                return;
            }
                
            var syntax = tables[0].GetQuerySyntaxHelper();

            //if user hasn't listed a specific datatype, guess it from the column 
            if(string.IsNullOrWhiteSpace(dataType))
            {
                var available = TagColumnAdder.GetAvailableTags();

                if(!available.Contains(column))
                {
                    var similar = available.Where(c=>c.Contains(column)).ToArray();

                    if(similar.Any())
                    {
                        SetImpossible($"Could not find a tag called '{column}'. Possibly  you meant:" + Environment.NewLine + string.Join(Environment.NewLine,similar));
                        return;
                    }
                        
                    SetImpossible($"Could not find a tag called '{column}' or any like it");
                    return;
                }

                try
                {
                    dataType = TagColumnAdder.GetDataTypeForTag(column,syntax.TypeTranslater);
                }
                catch (Exception e)
                {
                    throw new Exception("No dataType was specified and column name could not be resolved to a DicomTag",e);
                }
                
            }
                        
            _adder = new TagColumnAdder(column,dataType,(TableInfo)tables[0],new AcceptAllCheckNotifier());
            
        }
        public override void Execute()
        {
            base.Execute();

            _adder.Execute();
        }
    }
}
