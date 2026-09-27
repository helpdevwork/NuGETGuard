using System.Text.Json;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.VersionInfo;

public class VersionInfoService : IVersionInfoService
{
    private readonly HttpClient _httpClient;
    private string? _packageBaseAddress;

    public VersionInfoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<VersionFinding> GetVersionInfoAsync(string packageId, string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureServiceIndexLoaded(cancellationToken);

            var lowerId = packageId.ToLowerInvariant();
            var versionsUrl = $"{_packageBaseAddress}{lowerId}/index.json";

            using var response = await _httpClient.GetAsync(versionsUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new VersionFinding(packageId, currentVersion, currentVersion, true, null);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("versions", out var versionsArray))
            {
                return new VersionFinding(packageId, currentVersion, currentVersion, true, null);
            }

            var versions = versionsArray.EnumerateArray()
                .Select(v => v.GetString()!)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

            var isCurrentPrerelease = currentVersion.Contains('-');
            var stableVersions = isCurrentPrerelease
                ? versions
                : versions.Where(v => !v.Contains('-')).ToList();

            var latestVersion = stableVersions.LastOrDefault() ?? currentVersion;
            var isUpToDate = string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase);

            return new VersionFinding(packageId, currentVersion, latestVersion, isUpToDate, null);
        }
        catch (Exception)
        {
            return new VersionFinding(packageId, currentVersion, currentVersion, true, null);
        }
    }

    private async Task EnsureServiceIndexLoaded(CancellationToken cancellationToken)
    {
        if (_packageBaseAddress is not null)
            return;

        using var response = await _httpClient.GetAsync("https://api.nuget.org/v3/index.json", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        foreach (var resource in doc.RootElement.GetProperty("resources").EnumerateArray())
        {
            var type = resource.GetProperty("@type").GetString();
            if (type == "PackageBaseAddress/3.0.0")
            {
                _packageBaseAddress = resource.GetProperty("@id").GetString()!;
                if (!_packageBaseAddress.EndsWith('/'))
                    _packageBaseAddress += '/';
                return;
            }
        }

        _packageBaseAddress = "https://api.nuget.org/v3-flatcontainer/";
    }
}
