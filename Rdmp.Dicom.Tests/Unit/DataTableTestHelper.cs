using System.Data;
using System.Linq;

namespace Rdmp.Dicom.Tests.Unit;

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
        DataTable result = new();

        for (var i = 0; i < columnNames.Length; i++)
        {
            DataColumn column = new() { DataType = data[0, i].GetType(), ColumnName = columnNames[i] };
            result.Columns.Add(column);
        }

        for (var i = 0; i < data.GetLength(0); i++)
        {
            var row = result.NewRow();

            for (var j = 0; j < data.GetLength(1); j++)
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
        return expectedRow.Length == dataTable.Columns.Count && (from DataRow row in dataTable.Rows select !expectedRow.Where((t, i) => !t.Equals(row[i])).Any()).Any(matched => matched);
    }
}