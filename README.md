# RdmpDicom
Plugin for [RDMP](https://github.com/HicServices/RDMP) that adds support for load, linking (with EHR data in relational databases) and extracting anonymous DICOM images for researchers.


# Using Plugin

The following demo shows how RDMP dicom plugin works:
https://youtu.be/j42hmVZKRb4

Functionality is divided into the following areas:

- [Data Load](./Documentation/DataLoad.md)
- [Data Extraction](./Documentation/DataExtraction.md)
- [Caching (Fetching images from a DicomServer)](./Documentation/DataLoad.md)

The Plugin can be downloaded from the [releases section of GitHub](https://github.com/HicServices/RdmpDicom/releases) or you can build it yourself.

# Building

You can build Rdmp.Dicom as a plugin for RDMP by running the following:

```
dotnet build
dotnet publish .\Plugin\netcoreapp2.2\netcoreapp2.2.csproj -r win-x64
nuget pack Rdmp.Dicom.nuspec -Properties Configuration=Debug -IncludeReferencedProjects -Symbols -Version 0.0.1
```

This should produce a nuget package with runtimes compatible with RDMP client and CLI.
