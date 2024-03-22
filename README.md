[![Build status](https://github.com/HicServices/RdmpDicom/actions/workflows/dotnet-core.yml/badge.svg)](https://github.com/HicServices/RdmpDicom/actions/workflows/dotnet-core.yml) [![Total alerts](https://img.shields.io/lgtm/alerts/g/HicServices/RdmpDicom.svg?logo=lgtm&logoWidth=18)](https://lgtm.com/projects/g/HicServices/RdmpDicom/alerts/) [![NuGet Badge](https://buildstats.info/nuget/HIC.RDMP.Dicom)](https://buildstats.info/nuget/HIC.RDMP.Dicom)

# RdmpDicom
Plugin for [RDMP](https://github.com/HicServices/RDMP) that adds support for load, linking (with EHR data in relational databases) and extracting anonymous DICOM images for researchers.


# Using Plugin

The following demo shows how to deploy and use the RDMP dicom plugin:
https://youtu.be/j42hmVZKRb4

Releases of the Rdmp.Dicom plugin are hosted in the [Releases section of this Github Repository](https://github.com/HicServices/RdmpDicom/releases).  Once you have downloaded the plugin you can add it to your RDMP instance through the Plugins node in the Tables collection:

![Overview](./Documentation/Images/AddPlugin.png)

Once installed the following functionality is available:

- [Data Load](./Documentation/DataLoad.md)
- [Data Extraction](./Documentation/DataExtraction.md)
- [Caching (Fetching images from a DicomServer)](./Documentation/Caching.md)
- [NLP Cohort Building Plugin](./Documentation/NlpPlugin.md)

# Building

Building requires MSBuild 15 or later (or Visual Studio 2017 or later).  You will also need to install the [DotNetCore 2.2 SDK](https://dotnet.microsoft.com/download).

You can build Rdmp.Dicom as a plugin for RDMP by running the following (use the Version number in [SharedAssemblyInfo.cs](SharedAssemblyInfo.cs) in place of 0.0.1)

```bash
cd Plugin/windows
dotnet publish --runtime win-x64 -c Release --self-contained false
cd ../main
dotnet publish --runtime win-x64 -c Release --self-contained false
dotnet publish --runtime linux-x64 -c Release --self-contained false
cd ../..
nuget pack ./Rdmp.Dicom.nuspec -Properties Configuration=Release -IncludeReferencedProjects -Symbols -Version 0.0.1
```

This will produce a nupkg file (e.g. Rdmp.Dicom.0.0.1.rdmp) which can be consumed by both the RDMP client and dot net core RDMP CLI.

# Debugging
Since it is annoying to have to upload a new version of the plugin to test changes you can instead publish directly to the RDMP bin directory.

For example imagine that you have RDMP checked out and building in the following directory:
```
D:\Repos\RDMP\Application\ResearchDataManagementPlatform\bin\Debug\net6.0-windows
```

Build the RdmpDicom project to this directory:

```
cd D:\Repos\RdmpDicom
dotnet publish -o D:\Repos\RDMP\Application\ResearchDataManagementPlatform\bin\Debug\net6.0-windows -r win-x64
cd D:\Repos\RdmpDicom\Rdmp.Dicom.UI
dotnet publish -o D:\Repos\RDMP\Application\ResearchDataManagementPlatform\bin\Debug\net6.0-windows -r win-x64
```

Now run RDMP:
```
D:\Repos\RDMP\Application\ResearchDataManagementPlatform\bin\Debug\net6.0-windows\ResearchDataManagementPlatform.exe
```

Attach the visual studio debugger with `Debug=>Attach To Process...`



# New CLI Commands

This plugin adds the following commands to RDMP CLI:

```
./rdmp cmd CFind 2001-01-01 2020-01-01 www.dicomserver.co.uk 104 you me .
```
_Connects to the given PACS and writes CFind response for date range into output file (note that the . denotes current directory)_


```
./rdmp cmd PACSFetch 2001-01-01 2020-01-01 www.dicomserver.co.uk 104 you localhost 104 me . 0
```
_Connects to the given PACS and fetches all images between the date ranges (requires firewall allows incomming connections from destination server)_

# Troubleshooting
Ensure that you have the Rdmp.Dicom plugin installed and that it has loaded correctly.  Search for it with `Ctrl+F` and enter "Plugin".  If it is not appearing at all then it is not installed.  If it appears under 'Old Plugins' then your Rdmp.Dicom version is out of date and you will need to get the latest version from GitHub Releases.

Check the messages that appear when starting RDMP from the command line, e.g.:

`./rdmp.exe listsupportedcommands --logstartup > out.txt`

If using the RDMP Windows Client you can check that Rdmp.Dicom has loaded correctly by looking at the Types loaded.  Use the menu  `Diagnostics=>Plugins=>List All Types`.  This will generate a file with all the Types RDMP sees (~33,000) you should see all plugin Types here too, for example:

- Rdmp.Dicom.ExternalApis.SemEHRApiCaller
- Rdmp.Dicom.ExternalApis.SemEHRConfiguration
- Rdmp.Dicom.ExternalApis.SemEHRResponse
