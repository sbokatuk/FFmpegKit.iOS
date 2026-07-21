using System.IO.Compression;
using System.Text;

namespace FFmpegKit.iOS.PackageTests;

/// <summary>
/// Asserts the shape of the produced NuGet packages. These run against the packed .nupkg rather
/// than the build output, so they catch packaging regressions the compiler cannot see.
/// </summary>
public class PackageLayoutTests
{
    /// <summary>
    /// The heavyweight payload checks decompress tens of megabytes, and the payload is identical
    /// across target frameworks, so they run against one rather than all three. That the others
    /// carry the same bytes is asserted separately and cheaply by
    /// <see cref="Native_payload_is_identical_across_target_frameworks"/>.
    /// </summary>
    private const string PayloadTargetFramework = "net8.0-ios18.0";

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Package_carries_a_binding_assembly_for_every_target_framework(string variant)
    {
        using var package = Packages.OpenPackage(variant);

        foreach (var tfm in Packages.ExpectedTargetFrameworks)
        {
            var expected = $"lib/{tfm}/{Packages.AssemblyName(variant)}.dll";
            Assert.True(
                package.GetEntry(expected) is not null,
                $"{Packages.PackageId(variant)} is missing '{expected}'.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Package_carries_the_native_payload_for_every_target_framework(string variant)
    {
        using var package = Packages.OpenPackage(variant);

        foreach (var tfm in Packages.ExpectedTargetFrameworks)
        {
            var entry = package.GetEntry(Packages.ResourcesEntry(variant, tfm));

            // The payload must be a single .resources.zip rather than a .resources directory.
            // The iOS SDK emits the directory form unless CompressBindingResourcePackage is set,
            // which puts ~1000 files at paths long enough to break restore on Windows.
            Assert.True(
                entry is not null,
                $"{Packages.PackageId(variant)} is missing '{Packages.ResourcesEntry(variant, tfm)}'. " +
                "Has CompressBindingResourcePackage been unset?");

            // The native payload is tens of megabytes; anything tiny means an empty placeholder.
            Assert.True(entry!.Length > 10_000_000, $"'{entry.FullName}' is only {entry.Length} bytes.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Native_payload_is_the_same_across_target_frameworks(string variant)
    {
        using var package = Packages.OpenPackage(variant);

        // net8/net9 are packed by the .NET 9 SDK and net10 by the .NET 10 one, and
        // merge-packages.py then grafts the net10 lib/ tree into the other package. Nothing in
        // that flow guarantees the two passes bound the same variant or the same native version,
        // so this is where a mismatched graft would be caught.
        //
        // Compared by logical content, not bytes: each pass re-zips the payload and the archives
        // embed their own timestamps, so the same frameworks legitimately produce different CRCs.
        var manifests = new List<(string Tfm, List<(string Name, long Length)> Entries)>();

        foreach (var tfm in Packages.ExpectedTargetFrameworks)
        {
            // Opened and released one at a time - three FullGpl payloads at once would be ~230 MB.
            using var payload = Packages.OpenNativePayload(package, variant, tfm);
            manifests.Add((tfm, payload.Entries
                .Select(e => (e.FullName, e.Length))
                .OrderBy(e => e.FullName, StringComparer.Ordinal)
                .ToList()));
        }

        var reference = manifests[0];
        foreach (var (tfm, entries) in manifests.Skip(1))
        {
            Assert.True(
                reference.Entries.SequenceEqual(entries),
                $"The native payload for {tfm} differs from {reference.Tfm} in {Packages.PackageId(variant)}.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Native_payload_carries_every_xcframework(string variant)
    {
        using var package = Packages.OpenPackage(variant);
        using var payload = Packages.OpenNativePayload(package, variant, PayloadTargetFramework);

        var present = payload.Entries
            .Select(e => e.FullName.Split('/')[0])
            .Where(name => name.EndsWith(".xcframework", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var expected = Packages.ExpectedXcFrameworks
            .Select(name => $"{name}.xcframework")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, present);
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Native_payload_carries_ios_slices_only(string variant)
    {
        using var package = Packages.OpenPackage(variant);
        using var payload = Packages.OpenNativePayload(package, variant, PayloadTargetFramework);

        foreach (var framework in Packages.ExpectedXcFrameworks)
        {
            var slices = payload.Entries
                .Where(e => e.FullName.StartsWith($"{framework}.xcframework/", StringComparison.Ordinal))
                .Select(e => e.FullName.Split('/'))
                .Where(parts => parts.Length > 2)
                .Select(parts => parts[1])
                .Where(slice => !slice.EndsWith(".plist", StringComparison.Ordinal))
                .Distinct()
                .OrderBy(slice => slice, StringComparer.Ordinal)
                .ToList();

            // Device and simulator, and nothing else. A macos slice creeping back in would inflate
            // the package by ~40% - and FullGpl already sits close to the 250 MB nuget.org limit.
            Assert.Equal(Packages.ExpectedSlices.OrderBy(s => s, StringComparer.Ordinal), slices);
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Xcframework_manifests_match_the_slices_actually_shipped(string variant)
    {
        using var package = Packages.OpenPackage(variant);
        using var payload = Packages.OpenNativePayload(package, variant, PayloadTargetFramework);

        foreach (var framework in Packages.ExpectedXcFrameworks)
        {
            var manifest = payload.GetEntry($"{framework}.xcframework/Info.plist");
            Assert.True(manifest is not null, $"{framework}.xcframework has no Info.plist.");

            using var reader = new StreamReader(manifest!.Open());
            var text = reader.ReadToEnd();

            // Stripping the macos slice means rewriting AvailableLibraries to match. If the
            // directory is gone but the manifest still advertises it, the iOS SDK rejects the
            // whole xcframework - a failure that would only surface in a consuming app's build.
            Assert.DoesNotContain("macos", text, StringComparison.OrdinalIgnoreCase);

            foreach (var slice in Packages.ExpectedSlices)
            {
                Assert.Contains(slice, text, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Gpl_variants_ship_the_gpl_only_codecs_and_the_others_do_not(string variant)
    {
        using var package = Packages.OpenPackage(variant);
        using var payload = Packages.OpenNativePayload(package, variant, PayloadTargetFramework);

        // Every variant ships identically named xcframeworks, so - unlike the Android packages,
        // where the .aar file name carries the variant - nothing in the layout proves which build
        // is inside. This does: x264 is the GPL encoder that makes the -gpl variants GPL in the
        // first place, so its presence is exactly what the package's licence expression claims.
        // Packing a GPL build under an LGPL licence expression is the one packaging mistake here
        // with legal consequences for consumers, and it is otherwise invisible.
        using var libavcodec = Packages.ReadEntry(
            payload,
            $"libavcodec.xcframework/ios-arm64_arm64e/libavcodec.framework/libavcodec");

        var containsX264 = ContainsAscii(libavcodec, "libx264");

        if (Packages.IsGpl(variant))
        {
            Assert.True(containsX264, $"{Packages.PackageId(variant)} declares GPL but has no x264 - is it really a -gpl build?");
        }
        else
        {
            Assert.False(containsX264, $"{Packages.PackageId(variant)} declares LGPL but contains x264 - a GPL build has been packed under an LGPL licence.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Package_declares_the_expected_nuspec_metadata(string variant)
    {
        using var package = Packages.OpenPackage(variant);
        var nuspec = Packages.ReadNuspec(package, variant);

        string Value(string name) => nuspec.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() ?? string.Empty;

        Assert.Equal(Packages.PackageId(variant), Value("id"));
        Assert.NotEmpty(Value("version"));
        Assert.Equal(Packages.LicenseExpression(variant), Value("license"));
        Assert.Equal("icon.png", Value("icon"));
        Assert.Equal("README.md", Value("readme"));
        Assert.Contains("FFmpegKit", Value("description"), StringComparison.Ordinal);

        var dependencyGroups = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "group")
            .Select(e => e.Attribute("targetFramework")?.Value)
            .ToList();

        Assert.Equal(Packages.ExpectedTargetFrameworks.OrderBy(t => t), dependencyGroups.OrderBy(t => t));
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Package_ships_the_icon_and_readme_it_references(string variant)
    {
        using var package = Packages.OpenPackage(variant);

        Assert.True(package.GetEntry("icon.png") is not null, "icon.png is referenced but not packed.");
        Assert.True(package.GetEntry("README.md") is not null, "README.md is referenced but not packed.");
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Package_ships_both_licence_texts_it_is_covered_by(string variant)
    {
        using var package = Packages.OpenPackage(variant);

        var bindings = new StreamReader(Packages.ReadEntry(package, "licenses/LICENSE")).ReadToEnd();
        Assert.Contains("MIT License", bindings, StringComparison.OrdinalIgnoreCase);

        // The GPL and LGPL texts differ only subtly at a glance - the LGPL is titled "GNU LESSER
        // GENERAL PUBLIC LICENSE" - so assert both the file name and the title, to catch the two
        // being swapped as well as a variant being mapped to the wrong licence.
        var expectedFile = $"licenses/{Packages.NativeLicense(variant).Replace("-only", string.Empty)}.txt";
        var expectedTitle = Packages.IsGpl(variant)
            ? "GNU GENERAL PUBLIC LICENSE"
            : "GNU LESSER GENERAL PUBLIC LICENSE";

        var native = new StreamReader(Packages.ReadEntry(package, expectedFile)).ReadToEnd();

        Assert.StartsWith(expectedTitle, native.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("Version 3", native, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Packages.Variants), MemberType = typeof(Packages))]
    public void Symbol_package_is_produced(string variant)
    {
        using var symbols = Packages.OpenPackage(variant, ".snupkg");

        foreach (var tfm in Packages.ExpectedTargetFrameworks)
        {
            var expected = $"lib/{tfm}/{Packages.AssemblyName(variant)}.pdb";
            Assert.True(
                symbols.GetEntry(expected) is not null,
                $"Symbol package for {Packages.PackageId(variant)} is missing '{expected}'.");
        }
    }

    [Fact]
    public void Every_variant_is_packed_with_the_same_version()
    {
        var versions = Packages.Variants
            .Select(row => (string)row[0])
            .Select(variant =>
            {
                using var package = Packages.OpenPackage(variant);
                var nuspec = Packages.ReadNuspec(package, variant);
                return nuspec.Descendants().First(e => e.Name.LocalName == "version").Value.Trim();
            })
            .Distinct()
            .ToList();

        Assert.Single(versions);
    }

    /// <summary>
    /// Scans a native binary for an ASCII marker, in chunks so a ~40 MB library does not have to
    /// be turned into a string. Overlapping window, so a match spanning a chunk boundary is found.
    /// </summary>
    private static bool ContainsAscii(Stream stream, string marker)
    {
        var needle = Encoding.ASCII.GetBytes(marker);
        var overlap = needle.Length - 1;
        var buffer = new byte[1 << 20];
        var filled = 0;

        while (true)
        {
            var read = stream.Read(buffer, filled, buffer.Length - filled);
            if (read == 0)
            {
                return false;
            }

            filled += read;

            if (buffer.AsSpan(0, filled).IndexOf(needle) >= 0)
            {
                return true;
            }

            // Carry the tail forward so a marker straddling two reads is still matched.
            if (filled >= overlap)
            {
                buffer.AsSpan(filled - overlap, overlap).CopyTo(buffer);
                filled = overlap;
            }
        }
    }
}
