using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Detectors;

public interface ILicenseChangeDetector
{
    Task<IReadOnlyList<LicenseFinding>> DetectAsync(IReadOnlyList<PackageReference> packages, CancellationToken cancellationToken = default);
}
