using System.IO.Compression;
using System.Xml.Linq;

namespace FFmpegKit.iOS.PackageTests;

/// <summary>
/// Locates the packed .nupkg files and exposes the variants under test.
/// </summary>
public static class Packages
{
    /// <summary>Every FFmpegKit variant this repository builds.</summary>
    public static readonly string[] AllVariants =
    [
        "Audio", "Full", "FullGpl", "Https", "HttpsGpl", "Min", "MinGpl", "Video",
    ];

    /// <summary>Target frameworks each package must carry a binding assembly for.</summary>
    public static readonly string[] ExpectedTargetFrameworks =
    [
        "net8.0-ios18.0", "net9.0-ios18.0", "net10.0-ios26.0",
    ];

    /// <summary>
    /// The xcframeworks every variant ships: the FFmpegKit Objective-C API plus the seven FFmpeg
    /// libraries it links against.
    /// </summary>
    public static readonly string[] ExpectedXcFrameworks =
    [
        "ffmpegkit", "libavcodec", "libavdevice", "libavfilter",
        "libavformat", "libavutil", "libswresample", "libswscale",
    ];

    /// <summary>
    /// Identifies the simulator slice of an xcframework from its directory name.
    /// </summary>
    /// <remarks>
    /// Slice names are not asserted literally, because upstream changes them: the device slice
    /// was <c>ios-arm64_arm64e</c> until 8.1.2 was rebuilt as plain <c>ios-arm64</c>. What has to
    /// hold is structural - one device slice, one simulator slice, both iOS, no macOS - so that
    /// is what the tests check. The macOS slice upstream also ships is stripped by
    /// FetchXcFrameworks.sh: it cannot be reached from a net*-ios binding but would be embedded
    /// once per target framework, costing ~40% of the package for nothing.
    /// </remarks>
    public static bool IsSimulatorSlice(string slice) =>
        slice.Contains("simulator", StringComparison.Ordinal);

    /// <summary>Whether a slice directory name denotes an iOS slice at all.</summary>
    public static bool IsIosSlice(string slice) =>
        slice.StartsWith("ios-", StringComparison.Ordinal);

    public static string ArtifactsDirectory { get; } = ResolveArtifactsDirectory();

    /// <summary>
    /// Variants expected to be present. Defaults to all eight; narrow it with
    /// FFMPEGKIT_VARIANTS=Video when iterating locally on a single variant.
    /// </summary>
    public static IEnumerable<object[]> Variants =>
        (Environment.GetEnvironmentVariable("FFMPEGKIT_VARIANTS") is { Length: > 0 } filter
            ? filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : AllVariants)
        .Select(v => new object[] { v });

    public static string PackageId(string variant) => $"FFmpegKit.Net.{variant}.iOS";

    /// <summary>
    /// Upstream ships the -gpl variants under GPL-3.0 and the rest under LGPL-3.0. Getting this
    /// wrong would misrepresent the obligations a consumer takes on, so it is asserted per variant.
    /// </summary>
    public static bool IsGpl(string variant) => variant.EndsWith("Gpl", StringComparison.Ordinal);

    public static string NativeLicense(string variant) => IsGpl(variant) ? "GPL-3.0-only" : "LGPL-3.0-only";

    public static string LicenseExpression(string variant) => $"MIT AND {NativeLicense(variant)}";

    public static string AssemblyName(string variant) => $"FFmpegKit.Net.{variant}.iOS";

    /// <summary>
    /// The binding's native payload for a target framework: the xcframeworks, zipped by the iOS
    /// SDK into a binding resource package that sits beside the assembly.
    /// </summary>
    public static string ResourcesEntry(string variant, string targetFramework) =>
        $"lib/{targetFramework}/{AssemblyName(variant)}.resources.zip";

    public static string FindPackage(string variant, string extension = ".nupkg")
    {
        var id = PackageId(variant);
        var matches = Directory.Exists(ArtifactsDirectory)
            ? Directory.GetFiles(ArtifactsDirectory, $"{id}.*{extension}")
                .Where(f => !f.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray()
            : [];

        Assert.True(
            matches.Length > 0,
            $"No {id}*{extension} found in '{ArtifactsDirectory}'. " +
            "Run build/BuildNugets.sh (or the CI pack step) first.");

        // A rebuilt working copy can leave several versions behind; test the newest.
        return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    public static ZipArchive OpenPackage(string variant, string extension = ".nupkg") =>
        ZipFile.OpenRead(FindPackage(variant, extension));

    public static XDocument ReadNuspec(ZipArchive package, string variant)
    {
        var entry = package.GetEntry($"{PackageId(variant)}.nuspec");
        Assert.NotNull(entry);

        using var stream = entry!.Open();
        return XDocument.Load(stream);
    }

    /// <summary>Reads a package entry fully into memory so it can be seeked.</summary>
    public static MemoryStream ReadEntry(ZipArchive package, string entryName)
    {
        var entry = package.GetEntry(entryName);
        Assert.True(entry is not null, $"Package has no entry '{entryName}'.");

        var buffer = new MemoryStream();
        using (var stream = entry!.Open())
        {
            stream.CopyTo(buffer);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    /// Opens the binding resource package for a target framework. The native payload is a zip
    /// nested inside the .nupkg, so its contents are only reachable through a second archive.
    /// </summary>
    public static ZipArchive OpenNativePayload(ZipArchive package, string variant, string targetFramework) =>
        new(ReadEntry(package, ResourcesEntry(variant, targetFramework)));

    private static string ResolveArtifactsDirectory()
    {
        if (Environment.GetEnvironmentVariable("FFMPEGKIT_ARTIFACTS") is { Length: > 0 } configured)
        {
            return Path.GetFullPath(configured);
        }

        // Walk up to the repository root (the directory holding global.json).
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? AppContext.BaseDirectory, "artifacts");
    }
}
