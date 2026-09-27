using System.Text.Json;
using System.Text.Json.Serialization;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Policy;

public class PolicyFile
{
    [JsonPropertyName("failOn")]
    public string FailOn { get; set; } = "High";

    [JsonPropertyName("ignore")]
    public List<IgnoreEntry> Ignore { get; set; } = [];

    [JsonPropertyName("allowTyposquatWarningsOnly")]
    public bool AllowTyposquatWarningsOnly { get; set; } = true;

    [JsonPropertyName("checkLicenseChanges")]
    public bool CheckLicenseChanges { get; set; } = true;

    [JsonPropertyName("checkLatestVersion")]
    public bool CheckLatestVersion { get; set; } = true;

    public Severity FailOnSeverity => SeverityExtensions.Parse(FailOn);

    public bool ShouldIgnore(string packageId) =>
        Ignore.Any(i => string.Equals(i.PackageId, packageId, StringComparison.OrdinalIgnoreCase));

    public static PolicyFile Load(string? directory)
    {
        if (directory is null)
            return new PolicyFile();

        var path = Path.Combine(directory, ".nugetguard.json");
        if (!File.Exists(path))
            return new PolicyFile();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PolicyFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new PolicyFile();
    }

    public static void WriteDefault(string directory)
    {
        var path = Path.Combine(directory, ".nugetguard.json");
        var policy = new PolicyFile();
        var json = JsonSerializer.Serialize(policy, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(path, json);
    }
}

public class IgnoreEntry
{
    [JsonPropertyName("packageId")]
    public string PackageId { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}
