using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Detectors;

public interface ITyposquatDetector
{
    IReadOnlyList<TyposquatFinding> Detect(IReadOnlyList<PackageReference> packages);
}
