namespace NuGetGuard.Core.Models;

public record LicenseFinding(
    string PackageId,
    string VersionInstalled,
    string ChangedInVersion,
    string NewLicenseSummary,
    string SourceUrl
);
