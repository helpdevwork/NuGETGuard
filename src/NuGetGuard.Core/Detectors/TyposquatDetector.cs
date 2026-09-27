using System.Reflection;
using System.Text.Json;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Detectors;

public class TyposquatDetector : ITyposquatDetector
{
    private readonly HashSet<string> _popularPackages;
    private readonly List<string> _popularPackageList;

    public TyposquatDetector(string? dataFilePath = null)
    {
        var packages = LoadPopularPackages(dataFilePath);
        _popularPackages = new HashSet<string>(packages, StringComparer.OrdinalIgnoreCase);
        _popularPackageList = packages;
    }

    public IReadOnlyList<TyposquatFinding> Detect(IReadOnlyList<PackageReference> packages)
    {
        var findings = new List<TyposquatFinding>();

        foreach (var package in packages)
        {
            if (_popularPackages.Contains(package.Id))
                continue;

            foreach (var popular in _popularPackageList)
            {
                var distance = DamerauLevenshteinDistance(package.Id.ToLowerInvariant(), popular.ToLowerInvariant());

                if (distance > 0 && distance <= 2)
                {
                    findings.Add(new TyposquatFinding(
                        PackageId: package.Id,
                        LikelyIntendedId: popular,
                        EditDistance: distance,
                        Reason: $"Package name is {distance} edit(s) away from popular package '{popular}' — possible typosquat, review manually."
                    ));
                    break;
                }
            }
        }

        return findings;
    }

    private static int DamerauLevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;

        if (n == 0) return m;
        if (m == 0) return n;

        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);

                if (i > 1 && j > 1 && s[i - 1] == t[j - 2] && s[i - 2] == t[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + cost);
                }
            }
        }

        return d[n, m];
    }

    private static List<string> LoadPopularPackages(string? dataFilePath)
    {
        string jsonContent;

        if (dataFilePath is not null && File.Exists(dataFilePath))
        {
            jsonContent = File.ReadAllText(dataFilePath);
        }
        else
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("popular-packages.json"));

            if (resourceName is null)
                return [];

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            jsonContent = reader.ReadToEnd();
        }

        using var doc = JsonDocument.Parse(jsonContent);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return doc.RootElement.EnumerateArray()
                .Select(e =>
                {
                    if (e.ValueKind == JsonValueKind.String)
                        return e.GetString()!;
                    if (e.TryGetProperty("id", out var idProp))
                        return idProp.GetString()!;
                    return null;
                })
                .Where(s => s is not null)
                .Select(s => s!)
                .ToList();
        }

        return [];
    }
}
