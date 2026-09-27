namespace NuGetGuard.Core.Models;

public record ScanResult(
    IReadOnlyList<PackageReference> Packages,
    IReadOnlyList<VulnerabilityFinding> Vulnerabilities,
    IReadOnlyList<TyposquatFinding> Typosquats,
    IReadOnlyList<LicenseFinding> LicenseChanges,
    IReadOnlyList<VersionFinding> VersionInfo,
    DateTimeOffset ScannedAt
);
