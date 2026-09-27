using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.VersionInfo;

public interface IVersionInfoService
{
    Task<VersionFinding> GetVersionInfoAsync(string packageId, string currentVersion, CancellationToken cancellationToken = default);
}
