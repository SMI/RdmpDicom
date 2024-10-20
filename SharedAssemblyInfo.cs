using System.Reflection;
using System.Runtime.CompilerServices;

#if WINDOWS
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("windows")]
#endif

[assembly: AssemblyCompany("Health Informatics Centre, University of Dundee")]
[assembly: AssemblyProduct("RDMP Dicom Plugin")]
[assembly: AssemblyCopyright("Copyright (c) 2018 - 2024")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// These should be replaced with correct values by the release process
[assembly: AssemblyVersion("7.1.1")]
[assembly: AssemblyFileVersion("7.1.1")]
[assembly: AssemblyInformationalVersion("7.1.1")]
[assembly: InternalsVisibleTo("Rdmp.Dicom.Tests")]
