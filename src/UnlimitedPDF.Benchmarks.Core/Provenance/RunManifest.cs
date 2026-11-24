using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnlimitedPDF.Benchmarks.Core.Fixtures;

namespace UnlimitedPDF.Benchmarks.Core.Provenance;

public sealed record EnvironmentInfo(
    string OperatingSystem,
    string OsArchitecture,
    string ProcessArchitecture,
    string CpuModel,
    int LogicalCpuCount,
    int? PhysicalCpuCount,
    long TotalMemoryBytes,
    string RuntimeVersion,
    string? SdkVersion,
    string GcMode,
    bool ConcurrentGc,
    string GcLatencyMode,
    string BuildConfiguration,
    bool DebuggerAttached,
    string? PowerPlan,
    string MachineName)
{
    public static EnvironmentInfo Capture(Assembly entryAssembly)
    {
        var debuggable = entryAssembly.GetCustomAttribute<DebuggableAttribute>();
        var optimized = debuggable is null || !debuggable.IsJITOptimizerDisabled;
        return new EnvironmentInfo(
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            ReadCpuModel(),
            Environment.ProcessorCount,
            ReadPhysicalCores(),
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            RuntimeInformation.FrameworkDescription,
            Run("dotnet", "--version"),
            GCSettings.IsServerGC ? "Server" : "Workstation",
            AppContext.TryGetSwitch("System.GC.Concurrent", out var concurrent) ? concurrent : true,
            GCSettings.LatencyMode.ToString(),
            optimized ? "Release" : "Debug",
            Debugger.IsAttached,
            System.OperatingSystem.IsWindows() ? Run("powercfg", "/getactivescheme") : null,
            // A hash, not the name, so results can be grouped by machine without identifying it.
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName)))[..12]);
    }

    private static string ReadCpuModel()
    {
        try
        {
            if (System.OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string name)
                    return name.Trim();
            }
            else if (File.Exists("/proc/cpuinfo"))
            {
                var line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal));
                if (line is not null)
                    return line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
            }
            else if (System.OperatingSystem.IsMacOS())
            {
                return Run("sysctl", "-n machdep.cpu.brand_string") ?? "unknown";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
        return "unknown";
    }

    private static int? ReadPhysicalCores()
    {
        try
        {
            if (System.OperatingSystem.IsWindows())
            {
                var output = Run("powershell", "-NoProfile -Command \"(Get-CimInstance Win32_Processor | Measure-Object -Property NumberOfCores -Sum).Sum\"");
                return int.TryParse(output, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cores) ? cores : null;
            }
            if (File.Exists("/proc/cpuinfo"))
            {
                var ids = File.ReadLines("/proc/cpuinfo")
                    .Where(l => l.StartsWith("physical id", StringComparison.Ordinal) || l.StartsWith("core id", StringComparison.Ordinal))
                    .Chunk(2).Select(pair => string.Join('|', pair)).Distinct().Count();
                return ids > 0 ? ids : null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    internal static string? Run(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30_000);
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}

public sealed record GitInfo(string? Commit, bool? Dirty, string? Branch)
{
    public static GitInfo Capture(string repositoryRoot)
    {
        var commit = EnvironmentInfo.Run("git", $"-C \"{repositoryRoot}\" rev-parse HEAD");
        var status = EnvironmentInfo.Run("git", $"-C \"{repositoryRoot}\" status --porcelain");
        var branch = EnvironmentInfo.Run("git", $"-C \"{repositoryRoot}\" rev-parse --abbrev-ref HEAD");
        return new GitInfo(commit, status is null ? null : status.Length > 0, branch);
    }
}

public sealed record ComponentVersion(string Name, string Version, string Source);

/// <summary>Provenance recorded once per run and written to manifest.json.</summary>
public sealed record RunManifest(
    string RunId,
    DateTimeOffset StartedUtc,
    string Profile,
    IReadOnlyList<string> Arguments,
    GitInfo Git,
    EnvironmentInfo Environment,
    IReadOnlyList<ComponentVersion> Components,
    string FixtureVersion,
    string FixtureSha256,
    IReadOnlyDictionary<string, string> FontSha256)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static string NewRunId(DateTimeOffset now) =>
        $"{now.UtcDateTime:yyyyMMdd-HHmmss}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3))}";

    public static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "UnlimitedPDF.Benchmarks.slnx")))
                return dir.FullName;
        return Directory.GetCurrentDirectory();
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));

    public static RunManifest Load(string path) =>
        JsonSerializer.Deserialize<RunManifest>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Empty manifest.");
}

/// <summary>Assembly versions of loaded components (informational version, without the source-link hash).</summary>
public static class ComponentVersions
{
    public static ComponentVersion Of(string name, Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString() ?? "unknown";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return new ComponentVersion(name, plus > 0 ? version[..plus] : version, "assembly");
    }
}

/// <summary>Version and checksum of the deterministic fixtures, so results can be tied to their exact inputs.</summary>
public static class FixtureManifest
{
    public const string Version = "2026.11.1";

    public static IReadOnlyDictionary<string, string> FontChecksums() =>
        FontAssets.AllFaces.ToDictionary(f => FontAssets.FileName(f), FontAssets.Sha256);

    public static string Sha256()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string text) => hash.AppendData(Encoding.UTF8.GetBytes(text));
        foreach (var face in FontAssets.AllFaces)
            hash.AppendData(FontAssets.Bytes(face));
        foreach (var line in TextDocument.Create(2).Pages.SelectMany(p => p))
            Add(line.Text);
        foreach (var line in RichTextDocument.Create(1, true).Pages.SelectMany(p => p))
            Add(line.Text);
        hash.AppendData(ImageFixtures.Create(0, ImageFormat.Png).Encoded);
        hash.AppendData(ImageFixtures.Create(0, ImageFormat.Jpeg).Encoded);
        foreach (var row in TableFixtures.Simple(50).Rows)
            Add(string.Join('|', row.Cells));
        foreach (var kind in Enum.GetValues<BusinessDocumentKind>())
            foreach (var token in BusinessDocuments.Create(kind).RequiredText())
                Add(token);
        foreach (var language in Enum.GetValues<Language>())
            foreach (var line in MultilingualDocument.Create(language, 1).Pages[0])
                Add(line.Text);
        foreach (var kind in Enum.GetValues<HtmlKind>())
            Add(HtmlDocument.Create(kind, HtmlSize.Small).Html);
        foreach (var content in Enum.GetValues<FixtureContent>())
            hash.AppendData(FixturePdfWriter.Create(1, 5, content));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
