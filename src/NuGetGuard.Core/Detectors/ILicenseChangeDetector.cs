using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Detectors;

public interface ILicenseChangeDetector
{
    IReadOnlyList<LicenseFinding> Detect(IReadOnlyList<PackageReference> packages);
}
