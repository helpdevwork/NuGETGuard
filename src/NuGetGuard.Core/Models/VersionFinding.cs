namespace NuGetGuard.Core.Models;

public record VersionFinding(
    string PackageId,
    string CurrentVersion,
    string LatestVersion,
    bool IsUpToDate,
    string? ChangeSummary
);
