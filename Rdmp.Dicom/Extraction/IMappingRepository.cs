namespace Rdmp.Dicom.Extraction
{
    public interface IMappingRepository
    {
        UIDMapping[] LoadMappingsForProject(int projectNumber);
        void InsertMappings(UIDMapping[] newMappings);
        void Update(UIDMapping mapping);

        string GetOrAllocateMapping(string value, int projectNumber, UIDType uidType);
    }
}