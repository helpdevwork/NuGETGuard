using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Resolvers;

public class CompositePackageResolver : IPackageResolver
{
    public async Task<IReadOnlyList<PackageReference>> ResolveAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var directory = Directory.Exists(projectPath) ? projectPath : Path.GetDirectoryName(projectPath)!;
        var lockFiles = Directory.GetFiles(directory, "packages.lock.json", SearchOption.AllDirectories);

        IPackageResolver resolver = lockFiles.Length > 0
            ? new LockFilePackageResolver()
            : new ProjectPackageResolver();

        return await resolver.ResolveAsync(projectPath, cancellationToken);
    }
}
