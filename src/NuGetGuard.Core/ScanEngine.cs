using NuGetGuard.Core.Detectors;
using NuGetGuard.Core.Models;
using NuGetGuard.Core.Policy;
using NuGetGuard.Core.Resolvers;
using NuGetGuard.Core.VersionInfo;

namespace NuGetGuard.Core;

public class ScanEngine
{
    private readonly IPackageResolver _resolver;
    private readonly IVulnerabilityScanner _vulnerabilityScanner;
    private readonly ITyposquatDetector _typosquatDetector;
    private readonly ILicenseChangeDetector _licenseChangeDetector;
    private readonly IVersionInfoService _versionInfoService;

    public ScanEngine(
        IPackageResolver resolver,
        IVulnerabilityScanner vulnerabilityScanner,
        ITyposquatDetector typosquatDetector,
        ILicenseChangeDetector licenseChangeDetector,
        IVersionInfoService versionInfoService)
    {
        _resolver = resolver;
        _vulnerabilityScanner = vulnerabilityScanner;
        _typosquatDetector = typosquatDetector;
        _licenseChangeDetector = licenseChangeDetector;
        _versionInfoService = versionInfoService;
    }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken = default)
    {
        var packages = await _resolver.ResolveAsync(options.ProjectPath, cancellationToken);

        if (options.Policy is not null)
        {
            packages = packages
                .Where(p => !options.Policy.ShouldIgnore(p.Id))
                .ToList();
        }

        var vulnTask = _vulnerabilityScanner.ScanAsync(packages, cancellationToken);

        var typosquats = options.CheckTyposquat
            ? _typosquatDetector.Detect(packages)
            : (IReadOnlyList<TyposquatFinding>)[];

        var licenseTask = options.CheckLicenseChanges
            ? _licenseChangeDetector.DetectAsync(packages, cancellationToken)
            : Task.FromResult<IReadOnlyList<LicenseFinding>>([]);

        var versionInfoList = new List<VersionFinding>();
        if (options.CheckLatestVersion)
        {
            var tasks = packages.Select(p =>
                _versionInfoService.GetVersionInfoAsync(p.Id, p.ResolvedVersion, cancellationToken));
            var results = await Task.WhenAll(tasks);
            versionInfoList.AddRange(results);
        }

        var vulnerabilities = await vulnTask;
        var licenseChanges = await licenseTask;

        return new ScanResult(
            Packages: packages,
            Vulnerabilities: vulnerabilities,
            Typosquats: typosquats,
            LicenseChanges: licenseChanges,
            VersionInfo: versionInfoList,
            ScannedAt: DateTimeOffset.UtcNow
        );
    }
}

public class ScanOptions
{
    public required string ProjectPath { get; init; }
    public PolicyFile? Policy { get; init; }
    public bool CheckTyposquat { get; init; } = true;
    public bool CheckLicenseChanges { get; init; } = true;
    public bool CheckLatestVersion { get; init; } = true;
}
