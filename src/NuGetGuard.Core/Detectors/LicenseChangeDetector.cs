using System.Text.Json;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Detectors;

public class LicenseChangeDetector : ILicenseChangeDetector
{
    private readonly HttpClient _httpClient;
    private string? _registrationBaseUrl;

    public LicenseChangeDetector(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<LicenseFinding>> DetectAsync(IReadOnlyList<PackageReference> packages, CancellationToken cancellationToken = default)
    {
        var findings = new List<LicenseFinding>();
        await EnsureServiceIndexLoaded(cancellationToken);

        var tasks = packages.Select(p => CheckPackageLicenseAsync(p, cancellationToken));
        var results = await Task.WhenAll(tasks);

        foreach (var finding in results)
        {
            if (finding is not null)
                findings.Add(finding);
        }

        return findings;
    }

    private async Task<LicenseFinding?> CheckPackageLicenseAsync(PackageReference package, CancellationToken cancellationToken)
    {
        try
        {
            var lowerId = package.Id.ToLowerInvariant();
            var regUrl = $"{_registrationBaseUrl}{lowerId}/index.json";

            using var response = await _httpClient.GetAsync(regUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var catalogEntries = ExtractCatalogEntries(doc.RootElement);
            if (catalogEntries.Count < 2)
                return null;

            var currentEntry = catalogEntries
                .FirstOrDefault(e => string.Equals(e.Version, package.ResolvedVersion, StringComparison.OrdinalIgnoreCase));

            if (currentEntry is null)
                return null;

            var currentLicense = currentEntry.License;
            if (string.IsNullOrEmpty(currentLicense))
                return null;

            var previousVersions = catalogEntries
                .Where(e => CompareVersions(e.Version, package.ResolvedVersion) < 0)
                .OrderByDescending(e => e.Version, new VersionComparer())
                .ToList();

            if (previousVersions.Count == 0)
                return null;

            var previousEntry = previousVersions.First();
            var previousLicense = previousEntry.License;

            if (string.IsNullOrEmpty(previousLicense))
                return null;

            if (!string.Equals(currentLicense, previousLicense, StringComparison.OrdinalIgnoreCase))
            {
                return new LicenseFinding(
                    PackageId: package.Id,
                    VersionInstalled: package.ResolvedVersion,
                    ChangedInVersion: currentEntry.Version,
                    NewLicenseSummary: $"{previousLicense} → {currentLicense}",
                    SourceUrl: $"https://www.nuget.org/packages/{package.Id}/{currentEntry.Version}"
                );
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static List<CatalogEntry> ExtractCatalogEntries(JsonElement root)
    {
        var entries = new List<CatalogEntry>();

        if (!root.TryGetProperty("items", out var pages))
            return entries;

        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("items", out var items))
                continue;

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("catalogEntry", out var catalogEntry))
                    continue;

                var version = catalogEntry.TryGetProperty("version", out var vProp) ? vProp.GetString() : null;
                if (string.IsNullOrEmpty(version) || version.Contains('-'))
                    continue;

                var license = GetLicenseFromEntry(catalogEntry);

                entries.Add(new CatalogEntry(version, license ?? ""));
            }
        }

        return entries;
    }

    private static string? GetLicenseFromEntry(JsonElement entry)
    {
        if (entry.TryGetProperty("licenseExpression", out var expr))
        {
            var val = expr.GetString();
            if (!string.IsNullOrEmpty(val))
                return val;
        }

        if (entry.TryGetProperty("licenseUrl", out var url))
        {
            var val = url.GetString();
            if (!string.IsNullOrEmpty(val))
                return SummarizeLicenseUrl(val);
        }

        return null;
    }

    private static string SummarizeLicenseUrl(string url)
    {
        if (url.Contains("apache", StringComparison.OrdinalIgnoreCase) && url.Contains("2.0", StringComparison.OrdinalIgnoreCase))
            return "Apache-2.0";
        if (url.Contains("mit", StringComparison.OrdinalIgnoreCase))
            return "MIT";
        if (url.Contains("ms-pl", StringComparison.OrdinalIgnoreCase))
            return "MS-PL";
        if (url.Contains("bsd", StringComparison.OrdinalIgnoreCase))
            return "BSD";

        return url;
    }

    private static int CompareVersions(string v1, string v2)
    {
        var dash1 = v1.IndexOf('-');
        var dash2 = v2.IndexOf('-');
        var clean1 = dash1 >= 0 ? v1[..dash1] : v1;
        var clean2 = dash2 >= 0 ? v2[..dash2] : v2;

        if (Version.TryParse(clean1, out var ver1) && Version.TryParse(clean2, out var ver2))
            return ver1.CompareTo(ver2);

        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnsureServiceIndexLoaded(CancellationToken cancellationToken)
    {
        if (_registrationBaseUrl is not null)
            return;

        using var response = await _httpClient.GetAsync("https://api.nuget.org/v3/index.json", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        foreach (var resource in doc.RootElement.GetProperty("resources").EnumerateArray())
        {
            var type = resource.GetProperty("@type").GetString();
            if (type is "RegistrationsBaseUrl/3.6.0" or "RegistrationsBaseUrl")
            {
                _registrationBaseUrl = resource.GetProperty("@id").GetString()!;
                if (!_registrationBaseUrl.EndsWith('/'))
                    _registrationBaseUrl += '/';
                return;
            }
        }

        _registrationBaseUrl = "https://api.nuget.org/v3/registration5-gz-semver2/";
    }

    private record CatalogEntry(string Version, string License);

    private class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            return CompareVersions(x, y);
        }
    }
}
