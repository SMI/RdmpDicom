# RdmpDicom
Plugin for RDMP that adds support for load, linking (with EHR data in relational databases) and extracting anonymous DICOM images for researchers.

# Building

You can build Rdmp.Dicom as a plugin for RDMP by running the following:

```
dotnet build
dotnet publish .\Plugin\netcoreapp2.2\netcoreapp2.2.csproj -r win-x64
nuget pack Rdmp.Dicom.nuspec -Properties Configuration=Debug -IncludeReferencedProjects -Symbols -Version 0.0.1
```

This should produce a nuget package with runtimes compatible with RDMP client and CLI.
