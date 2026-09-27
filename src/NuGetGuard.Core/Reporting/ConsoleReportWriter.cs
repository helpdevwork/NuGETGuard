using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Reporting;

public class ConsoleReportWriter : IReportWriter
{
    public void Write(ScanResult result, TextWriter output)
    {
        output.WriteLine();
        output.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        output.WriteLine("║                    NuGetGuard Scan Report                    ║");
        output.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        output.WriteLine();
        output.WriteLine($"  Scanned {result.Packages.Count} package(s) at {result.ScannedAt:yyyy-MM-dd HH:mm:ss}");
        output.WriteLine();

        WriteVulnerabilities(result, output);
        WriteTyposquats(result, output);
        WriteLicenseChanges(result, output);
        WriteVersionInfo(result, output);
        WriteSummary(result, output);
    }

    private static void WriteVulnerabilities(ScanResult result, TextWriter output)
    {
        output.WriteLine("── Vulnerabilities ─────────────────────────────────────────────");

        if (result.Vulnerabilities.Count == 0)
        {
            output.WriteLine("  No known vulnerabilities found.");
            output.WriteLine();
            return;
        }

        var sorted = result.Vulnerabilities
            .OrderByDescending(v => SeverityExtensions.Parse(v.Severity))
            .ToList();

        foreach (var vuln in sorted)
        {
            output.WriteLine($"  [{vuln.Severity.ToUpperInvariant(),-8}] {vuln.PackageId} {vuln.Version}");
            output.WriteLine($"             {vuln.OsvId}: {Truncate(vuln.Summary, 80)}");
            if (vuln.FixedVersion is not null)
                output.WriteLine($"             Fix: upgrade to {vuln.FixedVersion}");
            output.WriteLine($"             {vuln.DetailsUrl}");
            output.WriteLine();
        }
    }

    private static void WriteTyposquats(ScanResult result, TextWriter output)
    {
        output.WriteLine("── Possible Typosquats ─────────────────────────────────────────");

        if (result.Typosquats.Count == 0)
        {
            output.WriteLine("  No typosquat warnings.");
            output.WriteLine();
            return;
        }

        foreach (var ts in result.Typosquats)
        {
            output.WriteLine($"  [WARNING ] {ts.PackageId}");
            output.WriteLine($"             Did you mean '{ts.LikelyIntendedId}'? (edit distance: {ts.EditDistance})");
            output.WriteLine($"             {ts.Reason}");
            output.WriteLine();
        }
    }

    private static void WriteLicenseChanges(ScanResult result, TextWriter output)
    {
        output.WriteLine("── License Changes ─────────────────────────────────────────────");

        if (result.LicenseChanges.Count == 0)
        {
            output.WriteLine("  No license change warnings.");
            output.WriteLine();
            return;
        }

        foreach (var lc in result.LicenseChanges)
        {
            output.WriteLine($"  [LICENSE ] {lc.PackageId} {lc.VersionInstalled}");
            output.WriteLine($"             License changed in v{lc.ChangedInVersion}: {lc.NewLicenseSummary}");
            output.WriteLine($"             Review: {lc.SourceUrl}");
            output.WriteLine();
        }
    }

    private static void WriteVersionInfo(ScanResult result, TextWriter output)
    {
        output.WriteLine("── Version Info ────────────────────────────────────────────────");

        if (result.VersionInfo.Count == 0)
        {
            output.WriteLine("  No version information available.");
            output.WriteLine();
            return;
        }

        var outdated = result.VersionInfo.Where(v => !v.IsUpToDate).ToList();
        var upToDate = result.VersionInfo.Where(v => v.IsUpToDate).ToList();

        if (outdated.Count > 0)
        {
            foreach (var vi in outdated)
            {
                output.WriteLine($"  [UPDATE  ] {vi.PackageId}: {vi.CurrentVersion} -> {vi.LatestVersion}");
                if (vi.ChangeSummary is not null)
                    output.WriteLine($"             {Truncate(vi.ChangeSummary, 80)}");
            }
            output.WriteLine();
        }

        output.WriteLine($"  {upToDate.Count} package(s) up to date, {outdated.Count} update(s) available.");
        output.WriteLine();
    }

    private static void WriteSummary(ScanResult result, TextWriter output)
    {
        output.WriteLine("── Summary ─────────────────────────────────────────────────────");
        output.WriteLine($"  Vulnerabilities:  {result.Vulnerabilities.Count}");
        output.WriteLine($"  Typosquat warns:  {result.Typosquats.Count}");
        output.WriteLine($"  License changes:  {result.LicenseChanges.Count}");
        output.WriteLine($"  Outdated:         {result.VersionInfo.Count(v => !v.IsUpToDate)}");
        output.WriteLine("════════════════════════════════════════════════════════════════");
        output.WriteLine();
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
}
