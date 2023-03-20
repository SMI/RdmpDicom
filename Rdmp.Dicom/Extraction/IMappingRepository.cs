namespace Rdmp.Dicom.Extraction;

public interface IMappingRepository
{
    string GetOrAllocateMapping(string value, int projectNumber, UIDType uidType);
}