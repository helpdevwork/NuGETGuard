using NuGetGuard.Core.Detectors;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Tests;

public class TyposquatDetectorTests
{
    private readonly TyposquatDetector _detector;

    public TyposquatDetectorTests()
    {
        _detector = new TyposquatDetector(FindDataFile("popular-packages.json"));
    }

    [Fact]
    public void ExactMatch_NoFinding()
    {
        var packages = new List<PackageReference>
        {
            new("Newtonsoft.Json", "13.0.3", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Empty(findings);
    }

    [Fact]
    public void CloseTypo_Flagged()
    {
        var packages = new List<PackageReference>
        {
            new("Newtonsoft.Jsn", "13.0.3", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Single(findings);
        Assert.Equal("Newtonsoft.Json", findings[0].LikelyIntendedId);
        Assert.True(findings[0].EditDistance <= 2);
    }

    [Fact]
    public void CompletelyDifferentName_NoFinding()
    {
        var packages = new List<PackageReference>
        {
            new("MyCompany.Internal.Utils", "1.0.0", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Empty(findings);
    }

    [Fact]
    public void MultiplePackages_OnlyFlagsTypos()
    {
        var packages = new List<PackageReference>
        {
            new("Newtonsoft.Json", "13.0.3", true, "test.csproj"),
            new("Serilog", "3.0.0", true, "test.csproj"),
            new("Serilogg", "1.0.0", true, "test.csproj")
        };

        var findings = _detector.Detect(packages);

        Assert.Single(findings);
        Assert.Equal("Serilogg", findings[0].PackageId);
        Assert.Equal("Serilog", findings[0].LikelyIntendedId);
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
