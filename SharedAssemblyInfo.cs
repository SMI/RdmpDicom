using System.Reflection;
using System.Runtime.CompilerServices;

#if WINDOWS
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("windows")]
#endif

[assembly: AssemblyCompany("Health Informatics Centre, University of Dundee")]
[assembly: AssemblyProduct("RDMP Dicom Plugin")]
[assembly: AssemblyCopyright("Copyright (c) 2018 - 2020")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// These should be replaced with correct values by the release process
[assembly: AssemblyVersion("7.0.2")]
[assembly: AssemblyFileVersion("7.0.2")]
[assembly: AssemblyInformationalVersion("7.0.2")]
[assembly: InternalsVisibleTo("Rdmp.Dicom.Tests")]
