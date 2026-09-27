namespace NuGetGuard.Core.Models;

public record TyposquatFinding(
    string PackageId,
    string LikelyIntendedId,
    double EditDistance,
    string Reason
);
