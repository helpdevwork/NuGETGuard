using System.Text.Json;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Resolvers;

public class LockFilePackageResolver : IPackageResolver
{
    public Task<IReadOnlyList<PackageReference>> ResolveAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var directory = Directory.Exists(projectPath) ? projectPath : Path.GetDirectoryName(projectPath)!;
        var lockFiles = Directory.GetFiles(directory, "packages.lock.json", SearchOption.AllDirectories);

        var packages = new List<PackageReference>();

        foreach (var lockFile in lockFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var refs = ParseLockFile(lockFile);
            packages.AddRange(refs);
        }

        var deduplicated = packages
            .GroupBy(p => $"{p.Id}|{p.ResolvedVersion}".ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        return Task.FromResult<IReadOnlyList<PackageReference>>(deduplicated);
    }

    private static IEnumerable<PackageReference> ParseLockFile(string lockFilePath)
    {
        using var stream = File.OpenRead(lockFilePath);
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("dependencies", out var dependencies))
            yield break;

        foreach (var framework in dependencies.EnumerateObject())
        {
            foreach (var package in framework.Value.EnumerateObject())
            {
                var packageId = package.Name;

                if (!package.Value.TryGetProperty("resolved", out var resolvedProp))
                    continue;

                var resolvedVersion = resolvedProp.GetString();
                if (string.IsNullOrEmpty(resolvedVersion))
                    continue;

                var isDirect = false;
                if (package.Value.TryGetProperty("type", out var typeProp))
                {
                    isDirect = string.Equals(typeProp.GetString(), "Direct", StringComparison.OrdinalIgnoreCase);
                }

                yield return new PackageReference(packageId, resolvedVersion, isDirect, lockFilePath);
            }
        }
    }
}
