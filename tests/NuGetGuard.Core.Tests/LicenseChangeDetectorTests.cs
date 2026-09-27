using NuGetGuard.Core.Detectors;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Tests;

public class LicenseChangeDetectorTests
{
    private readonly LicenseChangeDetector _detector;

    public LicenseChangeDetectorTests()
    {
        _detector = new LicenseChangeDetector(FindDataFile("license-changed.json"));
    }

    [Fact]
    public void PackageAboveChangeVersion_Flagged()
    {
        var packages = new List<PackageReference>
        {
            new("AutoMapper", "13.0.1", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Single(findings);
        Assert.Equal("AutoMapper", findings[0].PackageId);
        Assert.Equal("13.0.0", findings[0].ChangedInVersion);
    }

    [Fact]
    public void PackageAtExactChangeVersion_Flagged()
    {
        var packages = new List<PackageReference>
        {
            new("MediatR", "13.0.0", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Single(findings);
        Assert.Equal("MediatR", findings[0].PackageId);
    }

    [Fact]
    public void PackageBelowChangeVersion_NotFlagged()
    {
        var packages = new List<PackageReference>
        {
            new("AutoMapper", "12.0.1", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Empty(findings);
    }

    [Fact]
    public void UnknownPackage_NotFlagged()
    {
        var packages = new List<PackageReference>
        {
            new("SomeRandomPackage", "1.0.0", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Empty(findings);
    }

    [Fact]
    public void MultiplePackages_OnlyFlagsAffected()
    {
        var packages = new List<PackageReference>
        {
            new("AutoMapper", "14.0.0", true, "test.csproj"),
            new("Newtonsoft.Json", "13.0.3", true, "test.csproj"),
            new("EPPlus", "4.5.3", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Single(findings);
        Assert.Equal("AutoMapper", findings[0].PackageId);
    }

    private static string FindDataFile(string filename)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "data", filename);
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException($"Could not find {filename} in any parent directory.");
    }
}
