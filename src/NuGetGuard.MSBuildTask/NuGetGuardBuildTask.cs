using Microsoft.Build.Framework;

namespace NuGetGuard.MSBuildTask;

public class NuGetGuardBuildTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string ProjectPath { get; set; } = "";

    public string? FailOnSeverity { get; set; }

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal,
            $"NuGetGuard: scanning dependencies for {ProjectPath}...");

        try
        {
            var result = RunScan().GetAwaiter().GetResult();

            foreach (var vuln in result.Vulnerabilities)
            {
                var message = $"[{vuln.Severity}] {vuln.PackageId} {vuln.Version}: {vuln.Summary} ({vuln.OsvId})";
                if (IsBlockingSeverity(vuln.Severity))
                    Log.LogError(message);
                else
                    Log.LogWarning(message);
            }

            foreach (var ts in result.Typosquats)
            {
                Log.LogWarning($"Possible typosquat: '{ts.PackageId}' may be '{ts.LikelyIntendedId}' (distance: {ts.EditDistance})");
            }

            foreach (var lc in result.LicenseChanges)
            {
                Log.LogWarning($"License changed: {lc.PackageId} v{lc.ChangedInVersion}+ is now {lc.NewLicenseSummary}");
            }

            var hasBlocking = result.Vulnerabilities.Any(v => IsBlockingSeverity(v.Severity));
            return !hasBlocking;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"NuGetGuard scan failed: {ex.Message}");
            return true;
        }
    }

    private async Task<Core.Models.ScanResult> RunScan()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NuGetGuard/0.1.0");

        var engine = new Core.ScanEngine(
            new Core.Resolvers.CompositePackageResolver(),
            new Core.Detectors.VulnerabilityScanner(httpClient),
            new Core.Detectors.TyposquatDetector(),
            new Core.Detectors.LicenseChangeDetector(),
            new Core.VersionInfo.VersionInfoService(httpClient)
        );

        return await engine.ScanAsync(new Core.ScanOptions
        {
            ProjectPath = Path.GetDirectoryName(ProjectPath) ?? ProjectPath,
            CheckTyposquat = true,
            CheckLicenseChanges = true,
            CheckLatestVersion = false
        });
    }

    private bool IsBlockingSeverity(string severity)
    {
        var threshold = Core.Models.SeverityExtensions.Parse(FailOnSeverity ?? "High");
        var actual = Core.Models.SeverityExtensions.Parse(severity);
        return actual >= threshold && threshold != Core.Models.Severity.None;
    }
}
