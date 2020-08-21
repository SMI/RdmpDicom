using System.Data;

namespace Rdmp.Dicom.Tests
{
    /// <summary>
    /// Helper class for writing unit tests that use DataTable objects.
    /// </summary>
    class DataTableTestHelper
    {
        /// <summary>
        /// Creates a new DataTable object where all data values are strings.
        /// </summary>
        /// <param name="columnNames">names of the DataTable columns</param>
        /// <param name="data">data to include in the DataTable. For example { {"row1col1","row1col2","row1col3"},{"row2col1","row2col2","row2col3"}}</param>
        /// <returns>
        /// A DataTable containing the specified data.
        /// </returns>
        public static DataTable CreateDataTable(
            string[] columnNames,
            string[,] data
            )
        {
            DataTable result = new DataTable();

            for (int i = 0; i < columnNames.Length; i++)
            {
                DataColumn column = new DataColumn {DataType = data[0, i].GetType(), ColumnName = columnNames[i]};
                result.Columns.Add(column);
            }

            for (int i = 0; i < data.GetLength(0); i++)
            {
                DataRow row = result.NewRow();

                for (int j = 0; j < data.GetLength(1); j++)
                {
                    row[j] = data[i, j];
                }
                result.Rows.Add(row);
            }
            return result;
        }

        /// <summary>
        /// Checks to see if the data table contains the specified row.
        /// </summary>
        /// <param name="dataTable">the data table to check</param>
        /// <param name="expectedRow">
        /// Array of objects representing the contents of the row to be
        /// checked for.
        /// </param>
        /// <returns>
        /// true if the data table contains the specified row, false otherwise
        /// </returns>
        public static bool ContainsRow(DataTable dataTable, object[] expectedRow)
        {
            if (expectedRow.Length != dataTable.Columns.Count) return false;

            foreach (DataRow row in dataTable.Rows)
            {
                bool matched = true;
                for (int i = 0; i < expectedRow.Length; i++)
                {
                    if (!expectedRow[i].Equals(row[i]))
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched) return true;
            }
            return false;
        }
    }
}
