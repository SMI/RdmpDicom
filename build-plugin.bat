cd Plugin/net461
dotnet publish -r win-x64 -c Release
cd ..
cd netcoreapp2.2
dotnet publish -r win-x64 -c Release
dotnet publish -r linux-x64 -c Release
cd ../..
nuget pack Rdmp.Dicom.nuspec -Properties Configuration=Release -IncludeReferencedProjects -Symbols -Version %1

