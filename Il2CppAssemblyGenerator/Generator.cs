using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
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

namespace Il2CppAssemblyGenerator;

public class Generator
{
    public static void Generate(string OutPut, string Temp, UnityVersion Version)
    {
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

        Cpp2IlApi.InitializeLibCpp2Il(Temp + "/libil2pp", Temp + "/global-metadata.dat", Version, false);

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

        var version = $"{Version.Major}.{Version.Minor}.{Version.Build}";
        var source = "https://unity.bepinex.dev/libraries/{VERSION}.zip".Replace("{VERSION}", version);
        HttpClient Client = new HttpClient();

        Task<byte[]> download = Client.GetByteArrayAsync(source);
        byte[] data = download.GetAwaiter().GetResult();
        ZipArchive UnityAssemblies = new ZipArchive(new MemoryStream(data));
        string UnityLibs = Temp + "/UnityLibs";
        Directory.CreateDirectory(UnityLibs);
        foreach (var archive in UnityAssemblies.Entries)
        {
            File.WriteAllBytes(UnityLibs + "/" + archive.Name, archive.ReadBytes());
        }

        stopwatch.Stop();
        Logger.LogInfo($"Downloaded UnityLibs in {stopwatch.Elapsed}");

        var opts = new GeneratorOptions
        {
            GameAssemblyPath = Temp + "/libil2cpp.so",
            Source = assemblies,
            OutputDir = OutPut,
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
    }
}