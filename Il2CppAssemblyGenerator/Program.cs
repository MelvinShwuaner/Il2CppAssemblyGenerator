using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Extensions;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.OutputFormats;
using Cpp2IL.Core.ProcessingLayers;
using Il2CppInterop.Common;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using LibCpp2IL;

string? APK;

void Read()
{
    Logger.LogInfo("Please drag your apk onto the executable or input the path");
    APK = Console.ReadLine();
}
void Check()
{
    if (!File.Exists(APK))
    {
        Logger.LogInfo("invalid path!");
        APK = null;
    }
}
if (args.Length != 1)
{
    Read();
}
else
{
    APK = args[0];
}
Check();
while (APK == null)
{
    Read();
    Check();
}

string Folder = Path.GetDirectoryName(APK)!;
string Temp = Folder + "/Temp";
Directory.CreateDirectory(Temp);

MemoryStream ms = new MemoryStream(File.ReadAllBytes(APK));
ZipArchive Archive = new ZipArchive(ms);

var libil2cpp = Archive.GetEntry("lib/arm64-v8a/libil2cpp.so")!;
var metadata = Archive.GetEntry("assets/bin/Data/Managed/Metadata/global-metadata.dat")!.Open();
File.WriteAllBytes(Temp+"/libil2cpp.so", libil2cpp.ReadBytes());
File.WriteAllBytes(Temp+"/global-metadata.dat", metadata.ReadBytes());

var versiondata = Archive.GetEntry("assets/bin/Data/globalgamemanagers");
UnityVersion Version;
if (versiondata != null)
{
    Version = Cpp2IlApi.GetVersionFromGlobalGameManagers(versiondata.ReadBytes());
}
else
{
    versiondata = Archive.GetEntry("assets/bin/Data/data.unity3d");
    Version = Cpp2IlApi.GetVersionFromDataUnity3D(versiondata.Open());
}
Logger.LogInfo(Version);
InstructionSetRegistry.RegisterInstructionSet<NewArmV8InstructionSet>(DefaultInstructionSets.ARM_V8);
LibCpp2IlBinaryRegistry.RegisterBuiltInBinarySupport();

var stopwatch = new Stopwatch();
stopwatch.Start();

Cpp2IL.Core.Logging.Logger.VerboseLog += (message, s) =>
    Logger.LogDebug($"[{s}] {message.Trim()}");
Cpp2IL.Core.Logging.Logger.InfoLog += (message, s) =>
    Logger.LogInfo($"[{s}] {message.Trim()}");
Cpp2IL.Core.Logging.Logger.WarningLog += (message, s) =>
    Logger.LogWarning($"[{s}] {message.Trim()}");
Cpp2IL.Core.Logging.Logger.ErrorLog += (message, s) =>
    Logger.LogError($"[{s}] {message.Trim()}");

var unityVersion = Version;
Cpp2IlApi.InitializeLibCpp2Il(Temp+"/libil2cpp.so", Temp+"/global-metadata.dat", unityVersion, false);

List<Cpp2IlProcessingLayer> processingLayers = new() { new AttributeInjectorProcessingLayer(), };

foreach (var cpp2IlProcessingLayer in processingLayers)
{
    cpp2IlProcessingLayer.PreProcess(Cpp2IlApi.CurrentAppContext, processingLayers);
}

foreach (var cpp2IlProcessingLayer in processingLayers)
{
    cpp2IlProcessingLayer.Process(Cpp2IlApi.CurrentAppContext);
}

var assemblies = new AsmResolverDllOutputFormatDefault().BuildAssemblies(Cpp2IlApi.CurrentAppContext);

LibCpp2IlMain.Reset();
Cpp2IlApi.CurrentAppContext = null;

stopwatch.Stop();
Logger.LogInfo($"Cpp2IL finished in {stopwatch.Elapsed}");
Logger.LogInfo("Downloading UnityLibs");
stopwatch.Restart();

var version = $"{unityVersion.Major}.{unityVersion.Minor}.{unityVersion.Build}";
var source = "https://unity.bepinex.dev/libraries/{VERSION}.zip".Replace("{VERSION}", version);
HttpClient Client = new HttpClient();

Task<byte[]> download = Client.GetByteArrayAsync(source);
byte[] data = download.GetAwaiter().GetResult();
ZipArchive UnityAssemblies = new ZipArchive(new MemoryStream(data));
string UnityLibs = Temp + "/UnityLibs";
Directory.CreateDirectory(UnityLibs);
foreach (var archive in UnityAssemblies.Entries)
{
    File.WriteAllBytes(UnityLibs+"/"+archive.Name, archive.ReadBytes());
}
stopwatch.Stop();
Logger.LogInfo($"Downloaded UnityLibs in {stopwatch.Elapsed}");

var opts = new GeneratorOptions
{
    GameAssemblyPath = Temp+"/libil2cpp.so",
    Source = assemblies,
    OutputDir = Folder+"/Il2CppAssemblies",
    UnityBaseLibsDir = UnityLibs,
    ObfuscatedNamesRegex = new Regex("IL2CPP")
};


Logger.LogInfo("Generating interop assemblies");
stopwatch.Restart();

var logger = new Logger();

Il2CppInteropGenerator.Create(opts)
    .AddLogger(logger)
    .AddInteropAssemblyGenerator()
    .Run();
stopwatch.Stop();

Logger.LogInfo($"Generated interop assemblies in {stopwatch.Elapsed}");
Logger.LogInfo("Clearing Temp");
Directory.Delete(Temp, true);

