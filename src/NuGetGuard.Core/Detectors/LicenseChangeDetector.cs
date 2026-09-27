using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Detectors;

public class LicenseChangeDetector : ILicenseChangeDetector
{
    private readonly List<LicenseChangeEntry> _entries;

    public LicenseChangeDetector(string? dataFilePath = null)
    {
        _entries = LoadEntries(dataFilePath);
    }

    public IReadOnlyList<LicenseFinding> Detect(IReadOnlyList<PackageReference> packages)
    {
        var findings = new List<LicenseFinding>();

        foreach (var package in packages)
        {
            var entry = _entries.FirstOrDefault(e =>
                string.Equals(e.PackageId, package.Id, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
                continue;

            if (CompareVersions(package.ResolvedVersion, entry.ChangedInVersion) >= 0)
            {
                findings.Add(new LicenseFinding(
                    PackageId: package.Id,
                    VersionInstalled: package.ResolvedVersion,
                    ChangedInVersion: entry.ChangedInVersion,
                    NewLicenseSummary: entry.NewLicense,
                    SourceUrl: entry.SourceUrl
                ));
            }
        }

        return findings;
    }

    private static int CompareVersions(string v1, string v2)
    {
        if (Version.TryParse(StripPrerelease(v1), out var ver1) &&
            Version.TryParse(StripPrerelease(v2), out var ver2))
        {
            return ver1.CompareTo(ver2);
        }

        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripPrerelease(string version)
    {
        var dashIndex = version.IndexOf('-');
        return dashIndex >= 0 ? version[..dashIndex] : version;
    }

    private static List<LicenseChangeEntry> LoadEntries(string? dataFilePath)
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
                .FirstOrDefault(n => n.EndsWith("license-changed.json"));

            if (resourceName is null)
                return [];

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            jsonContent = reader.ReadToEnd();
        }

        return JsonSerializer.Deserialize<List<LicenseChangeEntry>>(jsonContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    private class LicenseChangeEntry
    {
        [JsonPropertyName("packageId")]
        public string PackageId { get; set; } = "";
        [JsonPropertyName("changedInVersion")]
        public string ChangedInVersion { get; set; } = "";
        [JsonPropertyName("previousLicense")]
        public string PreviousLicense { get; set; } = "";
        [JsonPropertyName("newLicense")]
        public string NewLicense { get; set; } = "";
        [JsonPropertyName("sourceUrl")]
        public string SourceUrl { get; set; } = "";
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
