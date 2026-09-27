namespace NuGetGuard.Core.Models;

public record PackageReference(
    string Id,
    string ResolvedVersion,
    bool IsDirect,
    string SourceFile
);
