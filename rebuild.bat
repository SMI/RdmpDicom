Z:\Repos\RDMP\Tools\rdmp\bin\Debug\net5.0\rdmp.exe cmd delete Plugin

RMDIR C:\Users\UKGC\AppData\Roaming\MEF

cd Plugin/windows
dotnet publish --runtime win-x64 -c Release --self-contained false
cd ../main
dotnet publish --runtime win-x64 -c Release --self-contained false
dotnet publish --runtime linux-x64 -c Release --self-contained false
cd ../..

Z:\Repos\RDMP\Tools\rdmp\bin\Debug\net6.0\rdmp.exe delete Plugin true

nuget.exe pack ./Rdmp.Dicom.nuspec -Properties Configuration=Release -IncludeReferencedProjects -Symbols -Version 0.0.1


Z:\Repos\RDMP\Tools\rdmp\bin\Debug\net6.0\rdmp.exe pack -f ./Rdmp.Dicom.0.0.1.nupkg

