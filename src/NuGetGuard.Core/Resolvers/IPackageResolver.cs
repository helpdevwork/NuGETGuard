using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Resolvers;

public interface IPackageResolver
{
    Task<IReadOnlyList<PackageReference>> ResolveAsync(string projectPath, CancellationToken cancellationToken = default);
}
