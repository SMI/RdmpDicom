using System;
using System.Linq;
using Dicom;
using DicomTypeTranslation;
using ReusableLibraryCode;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using FAnsi.Discovery;
using FAnsi.Discovery.TypeTranslation;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation;

namespace Rdmp.Dicom.TagPromotionSchema
{
    public class TagColumnAdder: BasicCommandExecution,ICheckable
    {
        /// <summary>
        /// The new tag (or transform column) the user wants to add to the table
        /// </summary>
        private readonly string _tagName;

        /// <summary>
        /// The data type the user requested to create for the Tag column
        /// </summary>
        private readonly string _datatype;

        /// <summary>
        /// The pointer in the RDMP data catalogue that references the table on your server (stores the location, known columns etc).
        /// </summary>
        private readonly TableInfo _tableInfo;

        private readonly ICheckNotifier _notifierForExecute;

        public TagColumnAdder(string tagName, string datatype, TableInfo table, ICheckNotifier notifierForExecute)
        {
            _tagName = tagName;
            _datatype = datatype;
            _tableInfo = table;
            _notifierForExecute = notifierForExecute;
        }

        public bool SkipChecksAndSynchronization { get; set; }

        public override void Execute()
        {
            base.Execute();

            if(!SkipChecksAndSynchronization)
                Check(_notifierForExecute);

            var db = GetDatabase();
            
            using (var con = db.Server.GetConnection())
            {
                con.Open();

                var table = db.ExpectTable(_tableInfo.GetRuntimeName());
                var archiveTable = db.ExpectTable(_tableInfo.GetRuntimeName(LoadBubble.Archive));

                table.AddColumn(_tagName, _datatype, true, DatabaseCommandHelper.GlobalTimeout);

                if (archiveTable.Exists())
                    archiveTable.AddColumn(_tagName, _datatype, true, DatabaseCommandHelper.GlobalTimeout);
            }

            if (!SkipChecksAndSynchronization)
                new TableInfoSynchronizer(_tableInfo).Synchronize(_notifierForExecute);
        }

        public void Check(ICheckNotifier notifier)
        {
            //synchronize the TableInfo
            new TableInfoSynchronizer(_tableInfo).Synchronize(notifier);

            if (_tableInfo.ColumnInfos.Any(c => c.GetRuntimeName().Equals(_tagName)))
            {
                notifier.OnCheckPerformed(new CheckEventArgs("There is already a column called '" + _tagName + "' in TableInfo " + _tableInfo,CheckResult.Fail));
                return;
            }

            var db = GetDatabase();
            try
            {
                db.Server.GetQuerySyntaxHelper().TypeTranslater.GetCSharpTypeForSQLDBType(_datatype);
                notifier.OnCheckPerformed(new CheckEventArgs("Datatype is compatible with TypeTranslater",CheckResult.Success));
            }
            catch (Exception ex)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Datatype '" + _datatype + "' is not supported",CheckResult.Fail, ex));
            }
        }

        private DiscoveredDatabase GetDatabase()
        {
            return DataAccessPortal.GetInstance().ExpectDatabase(_tableInfo, DataAccessContext.InternalDataProcessing);
        }

        public static string[] GetAvailableTags()
        {
            return DicomDictionary.Default.Select(t => t.Keyword).ToArray();
        }

        public static DicomDictionaryEntry GetTag(string keyword)
        {
            return DicomDictionary.Default.FirstOrDefault(t => t.Keyword == keyword);
        }

        public static string GetDataTypeForTag(string keyword,ITypeTranslater tt)
        {
            var tag = DicomDictionary.Default.FirstOrDefault(t => t.Keyword == keyword);

            if(tag == null)
                throw new NotSupportedException("Keyword '"+keyword + "' is not a valid Dicom Tag.");
            
            var type = DicomTypeTranslater.GetNaturalTypeForVr(tag.ValueRepresentations, tag.ValueMultiplicity);
            return tt.GetSQLDBTypeForCSharpType(type);
        }
    }
}
