using Microsoft.Data.SqlClient;

namespace Rdmp.Dicom.Extraction;

public interface IMappingRepository
{
    string GetOrAllocateMapping(SqlConnection con,string value, int projectNumber, UIDType uidType);
}