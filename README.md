# RdmpDicom
Plugin for [RDMP](https://github.com/HicServices/RDMP) that adds support for load, linking (with EHR data in relational databases) and extracting anonymous DICOM images for researchers.


# Using Plugin

The following demo shows how RDMP dicom plugin works:
https://www.youtube.com/watch?v=DOdU5jOtOKc

Functionality is divided into the following areas:

- [Data Load](./Documentation/DataLoad.md)
- [Data Extraction](./Documentation/DataLoad.md)
- [Caching (Fetching images from a DicomServer)](./Documentation/DataLoad.md)

# Building

You can build Rdmp.Dicom as a plugin for RDMP by running the following:

```
dotnet build
dotnet publish .\Plugin\netcoreapp2.2\netcoreapp2.2.csproj -r win-x64
nuget pack Rdmp.Dicom.nuspec -Properties Configuration=Debug -IncludeReferencedProjects -Symbols -Version 0.0.1
```

This should produce a nuget package with runtimes compatible with RDMP client and CLI.
