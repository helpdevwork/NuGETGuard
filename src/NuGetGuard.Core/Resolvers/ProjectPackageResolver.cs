using System.Xml.Linq;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Resolvers;

public class ProjectPackageResolver : IPackageResolver
{
    public Task<IReadOnlyList<PackageReference>> ResolveAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var packages = new List<PackageReference>();
        var directory = Directory.Exists(projectPath) ? projectPath : Path.GetDirectoryName(projectPath)!;

        var csprojFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories);
        var directoryPackagesProps = Path.Combine(directory, "Directory.Packages.props");

        var centralVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(directoryPackagesProps))
        {
            centralVersions = ParseCentralVersions(directoryPackagesProps);
        }

        foreach (var csproj in csprojFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var refs = ParseCsproj(csproj, centralVersions);
            packages.AddRange(refs);
        }

        var deduplicated = packages
            .GroupBy(p => $"{p.Id}|{p.ResolvedVersion}".ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        return Task.FromResult<IReadOnlyList<PackageReference>>(deduplicated);
    }

    private static Dictionary<string, string> ParseCentralVersions(string propsPath)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var doc = XDocument.Load(propsPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        foreach (var item in doc.Descendants(ns + "PackageVersion"))
        {
            var include = item.Attribute("Include")?.Value;
            var version = item.Attribute("Version")?.Value ?? item.Element(ns + "Version")?.Value;
            if (!string.IsNullOrEmpty(include) && !string.IsNullOrEmpty(version))
            {
                versions[include] = version;
            }
        }

        return versions;
    }

    private static IEnumerable<PackageReference> ParseCsproj(string csprojPath, Dictionary<string, string> centralVersions)
    {
        var doc = XDocument.Load(csprojPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        foreach (var item in doc.Descendants(ns + "PackageReference"))
        {
            var include = item.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
                continue;

            var version = item.Attribute("Version")?.Value ?? item.Element(ns + "Version")?.Value;

            if (string.IsNullOrEmpty(version))
                centralVersions.TryGetValue(include, out version);

            if (string.IsNullOrEmpty(version))
                continue;

            yield return new PackageReference(include, version, IsDirect: true, SourceFile: csprojPath);
        }
    }
}
