using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using Rdmp.Core.ReusableLibraryCode;
using FAnsi.Discovery;
using Microsoft.Data.SqlClient;
using Rdmp.Core.Curation.Data;

namespace Rdmp.Dicom.Extraction;

public class MappingRepository : IMappingRepository
{
    private readonly DiscoveredServer _server;
    private readonly DiscoveredDatabase _database;
    private readonly string _tableName = "UIDMapping"; // todo: inject

    public MappingRepository(ExternalDatabaseServer server)
    {
        _server = DiscoverServer(server);
        _database = _server.ExpectDatabase(server.Database);
    }

    public MappingRepository(DiscoveredServer server, string databaseName)
    {
        _server = server;
        _database = _server.ExpectDatabase(databaseName);
    }

    private DiscoveredServer DiscoverServer(ExternalDatabaseServer server)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server.Server
        };

        if (server.Username == null)
            builder.IntegratedSecurity = true;
        else
        {
            builder.UserID = server.Username;
            builder.Password = server.GetDecryptedPassword();
        }

        return new DiscoveredServer(builder);
    }

    private SqlConnection GetConnection()
    {
        _server.ChangeDatabase(_database.GetRuntimeName());
        return (SqlConnection) _server.GetConnection();
    }

    public UIDMapping[] LoadMappingsForProject(int projectNumber)
    {
        var table = _database.ExpectTable(_tableName);
        using var conn = GetConnection();
        conn.Open();
        var cmd =
            DatabaseCommandHelper.GetCommand(
                $"SELECT * FROM {table.GetFullyQualifiedName()} WHERE ProjectNumber = @ProjectNumber", conn);
        DatabaseCommandHelper.AddParameterWithValueToCommand("@ProjectNumber", cmd, projectNumber);

        var reader = cmd.ExecuteReader();
        var mappings = new List<UIDMapping>();
        while (reader.Read())
        {
            var mappingFromDatabase = HydrateMapping(reader);
            mappings.Add(mappingFromDatabase);
        }

        return mappings.ToArray();
    }

    private UIDMapping HydrateMapping(DbDataReader reader)
    {
        return new UIDMapping
        {
            PrivateUID = reader["PrivateUID"].ToString(),
            ReleaseUID = reader["ReleaseUID"].ToString(),
            ProjectNumber = Convert.ToInt32(reader["ProjectNumber"]),
            UIDType = (UIDType) Enum.Parse(typeof (UIDType), reader["UIDType"].ToString()),
            IsExternalReference = (bool) reader["IsExternalReference"]
        };
    }

    public void InsertMappings(SqlConnection conn,params UIDMapping[] newMappings)
    {
        // Set up a bulk insert
        var table = _database.ExpectTable(_tableName);

        // Create data table
        using var dt = new DataTable(_tableName);
        dt.BeginLoadData();

        using var da = new SqlDataAdapter(table.GetTopXSql(0), conn);
        da.Fill(dt);

        // Fill up the data table
        foreach (var mapping in newMappings)
        {
            var row = dt.NewRow();
            row["PrivateUID"] = mapping.PrivateUID;
            row["ReleaseUID"] = mapping.ReleaseUID;
            row["ProjectNumber"] = mapping.ProjectNumber;
            row["UIDType"] = mapping.UIDType;
            row["IsExternalReference"] = mapping.IsExternalReference;
            dt.Rows.Add(row);
        }

        // Perform the bulk copy
        dt.EndLoadData();
        using var bulkCopy = table.BeginBulkInsert();
        bulkCopy.Upload(dt);
    }

    public void Update(UIDMapping mapping)
    {

        var table = _database.ExpectTable(_tableName);
        var sql =
            $"UPDATE {table.GetFullyQualifiedName()} SET PrivateUID = @PrivateUID, ProjectNumber = @ProjectNumber, UIDType = @UIDType, IsExternalReference = @IsExternalReference WHERE ReleaseUID = @ReleaseUID";

        using var conn = GetConnection();
        conn.Open();
        var cmd = _server.GetCommand(sql, conn);
        _server.AddParameterWithValueToCommand("@PrivateUID", cmd, mapping.PrivateUID);
        _server.AddParameterWithValueToCommand("@ProjectNumber", cmd, mapping.ProjectNumber);
        _server.AddParameterWithValueToCommand("@UIDType", cmd, mapping.UIDType);
        _server.AddParameterWithValueToCommand("@IsExternalReference", cmd, mapping.IsExternalReference);
        _server.AddParameterWithValueToCommand("@ReleaseUID", cmd, mapping.ReleaseUID);

        cmd.ExecuteNonQuery();
    }

    public string GetOrAllocateMapping(SqlConnection con,string value, int projectNumber, UIDType uidType)
    {
        var cmd =
            _server.GetCommand(
                "SELECT ReleaseUID from UIDMapping WHERE ProjectNumber = @ProjectNumber AND UIDType = @UIDType AND PrivateUID = @PrivateUID",
                con);

        _server.AddParameterWithValueToCommand("@ProjectNumber", cmd, projectNumber);
        _server.AddParameterWithValueToCommand("@UIDType", cmd, uidType);
        _server.AddParameterWithValueToCommand("@PrivateUID", cmd, value);

        var result = cmd.ExecuteScalar();

        if (result != DBNull.Value && result != null) return result.ToString();
        var m = new UIDMapping
        {
            UIDType = uidType,
            ProjectNumber = projectNumber,
            PrivateUID = value,
            ReleaseUID = GetKindaUid(),
            IsExternalReference = false
        };

        InsertMappings(con,m);

        return m.ReleaseUID;
    }

    private static readonly Random r = new();
        
    private static string GetKindaUid()
    {
        StringBuilder sb = new("2.25.");
        while (sb.Length < 56)
        {
            var d = r.Next(int.MaxValue);
            sb.Append(d);
        }

        return sb.ToString(0, 56);
    }
}