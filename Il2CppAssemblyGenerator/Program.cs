using System.IO.Compression;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Extensions;
using Il2CppAssemblyGenerator;
string type = args[0];
UnityVersion Version;
string Temp;
string Folder;
switch (type)
{
    case "apk":
        GenerateFromApk();
        break;
    case "gradle":
        GenerateFromGradle();
        break;
    case "app":
         GenerateFromApp();
         break;
    default:
        throw new NotSupportedException("invalid type: " + type);
}
Generator.Generate(Folder+"/Il2CppAssemblies", Temp, Version);
return;

void GenerateFromApk()
{
    string APK = args[1];
    Folder = Path.GetDirectoryName(APK)!;
    Temp = Folder + "/TempGen";
    Directory.CreateDirectory(Temp);

    MemoryStream ms = new MemoryStream(File.ReadAllBytes(APK));
    ZipArchive Archive = new ZipArchive(ms);

    var libil2cpp = Archive.GetEntry("lib/arm64-v8a/libil2cpp.so")!;
    var metadata = Archive.GetEntry("assets/bin/Data/Managed/Metadata/global-metadata.dat")!.Open();
    File.WriteAllBytes(Temp + "/libil2cpp", libil2cpp.ReadBytes());
    File.WriteAllBytes(Temp + "/global-metadata.dat", metadata.ReadBytes());
    
    var versiondata = Archive.GetEntry("assets/bin/Data/globalgamemanagers");
    if (versiondata != null)
    {
        Version = Cpp2IlApi.GetVersionFromGlobalGameManagers(versiondata.ReadBytes());
    }
    else
    {
        versiondata = Archive.GetEntry("assets/bin/Data/data.unity3d")!;
        Version = Cpp2IlApi.GetVersionFromDataUnity3D(versiondata.Open());
    }
}


void GenerateFromGradle()
{
    string folder = args[1];
    Folder = Directory.GetParent(folder)!.FullName;
    Temp = Path.Combine(Folder, "TempGen");
    Directory.CreateDirectory(Temp);
    File.Copy(folder+"/src/main/jniLibs/arm64-v8a/libil2cpp.so", Temp + "/libil2cpp");
 
    string assetsDir = Path.Combine(folder, "src", "main", "assets");
    File.Copy(assetsDir+"/bin/Data/Managed/Metadata/global-metadata.dat", Temp + "/global-metadata.dat");
    string versiondata = assetsDir + "/bin/Data/globalgamemanagers";
    if (File.Exists(versiondata))
    {
        Version = Cpp2IlApi.GetVersionFromGlobalGameManagers(File.ReadAllBytes(versiondata));
    }
    else
    {
        versiondata = assetsDir + "/bin/Data/data.unity3d";
        Version = Cpp2IlApi.GetVersionFromDataUnity3D(File.OpenRead(versiondata));
    }
}
// .app folder
void GenerateFromApp()
{
    string folder = args[1];

    Folder = Directory.GetParent(folder)!.FullName;
    Temp = Path.Combine(Folder, "TempGen");
    Directory.CreateDirectory(Temp);
    
    File.Copy(Path.Combine(folder, "Frameworks/UnityFramework.framework/UnityFramework"), Path.Combine(Temp, "libil2cpp"));
    File.Copy(Path.Combine(folder, "Data/Managed/Metadata/global-metadata.dat"), Path.Combine(Temp, "global-metadata.dat"));

    string versiondata = Path.Combine(folder, "Data/globalgamemanagers");
    if (File.Exists(versiondata))
    {
        Version = Cpp2IlApi.GetVersionFromGlobalGameManagers(File.ReadAllBytes(versiondata));
    }
    else
    {
        versiondata = Path.Combine(folder, "Data/data.unity3d");
        Version = Cpp2IlApi.GetVersionFromDataUnity3D(File.OpenRead(versiondata));
    }
}