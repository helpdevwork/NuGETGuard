using NuGetGuard.Core.Models;
using NuGetGuard.Core.Reporting;

namespace NuGetGuard.Core.Tests;

public class ConsoleReportWriterTests
{
    [Fact]
    public void Write_EmptyScan_ProducesCleanOutput()
    {
        var result = new ScanResult(
            Packages: [new PackageReference("Newtonsoft.Json", "13.0.3", true, "test.csproj")],
            Vulnerabilities: [],
            Typosquats: [],
            LicenseChanges: [],
            VersionInfo: [new VersionFinding("Newtonsoft.Json", "13.0.3", "13.0.3", true, null)],
            ScannedAt: DateTimeOffset.UtcNow
        );

        var writer = new ConsoleReportWriter();
        using var sw = new StringWriter();
        writer.Write(result, sw);
        var output = sw.ToString();

        Assert.Contains("NuGetGuard Scan Report", output);
        Assert.Contains("Scanned 1 package(s)", output);
        Assert.Contains("No known vulnerabilities found", output);
        Assert.Contains("No typosquat warnings", output);
        Assert.Contains("No license change warnings", output);
        Assert.Contains("1 package(s) up to date", output);
    }

    [Fact]
    public void Write_WithVulnerability_ShowsDetails()
    {
        var result = new ScanResult(
            Packages: [new PackageReference("OldLib", "1.0.0", true, "test.csproj")],
            Vulnerabilities: [new VulnerabilityFinding("OldLib", "1.0.0", "GHSA-1234", "High", "Remote code execution", "2.0.0", "https://osv.dev/vulnerability/GHSA-1234")],
            Typosquats: [],
            LicenseChanges: [],
            VersionInfo: [],
            ScannedAt: DateTimeOffset.UtcNow
        );

        var writer = new ConsoleReportWriter();
        using var sw = new StringWriter();
        writer.Write(result, sw);
        var output = sw.ToString();

        Assert.Contains("HIGH", output);
        Assert.Contains("OldLib", output);
        Assert.Contains("GHSA-1234", output);
        Assert.Contains("Remote code execution", output);
        Assert.Contains("upgrade to 2.0.0", output);
    }
}
