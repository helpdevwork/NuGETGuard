using System.Text.Json;
using System.Text.Json.Serialization;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Reporting;

public class SarifReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Write(ScanResult result, TextWriter output)
    {
        var sarif = new SarifLog
        {
            Schema = "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/main/sarif-2.1/schema/sarif-schema-2.1.0.json",
            Version = "2.1.0",
            Runs =
            [
                new SarifRun
                {
                    Tool = new SarifTool
                    {
                        Driver = new SarifDriver
                        {
                            Name = "NuGetGuard",
                            Version = "0.1.0",
                            InformationUri = "https://github.com/nugetguard/nugetguard",
                            Rules = BuildRules(result)
                        }
                    },
                    Results = BuildResults(result)
                }
            ]
        };

        var json = JsonSerializer.Serialize(sarif, SerializerOptions);
        output.Write(json);
    }

    private static List<SarifRule> BuildRules(ScanResult result)
    {
        var rules = new List<SarifRule>();
        var seen = new HashSet<string>();

        foreach (var vuln in result.Vulnerabilities)
        {
            if (seen.Add(vuln.OsvId))
            {
                rules.Add(new SarifRule
                {
                    Id = vuln.OsvId,
                    ShortDescription = new SarifMessage { Text = vuln.Summary },
                    HelpUri = vuln.DetailsUrl,
                    DefaultConfiguration = new SarifConfiguration
                    {
                        Level = MapSeverityToLevel(vuln.Severity)
                    }
                });
            }
        }

        return rules;
    }

    private static List<SarifResult> BuildResults(ScanResult result)
    {
        var results = new List<SarifResult>();

        foreach (var vuln in result.Vulnerabilities)
        {
            var message = $"{vuln.PackageId} {vuln.Version} has a known vulnerability: {vuln.Summary}";
            if (vuln.FixedVersion is not null)
                message += $" (fix: upgrade to {vuln.FixedVersion})";

            results.Add(new SarifResult
            {
                RuleId = vuln.OsvId,
                Level = MapSeverityToLevel(vuln.Severity),
                Message = new SarifMessage { Text = message },
                Locations =
                [
                    new SarifLocation
                    {
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation
                            {
                                Uri = "project.csproj"
                            }
                        }
                    }
                ]
            });
        }

        foreach (var ts in result.Typosquats)
        {
            results.Add(new SarifResult
            {
                RuleId = "NUGETGUARD-TYPOSQUAT",
                Level = "warning",
                Message = new SarifMessage
                {
                    Text = $"Package '{ts.PackageId}' may be a typosquat of '{ts.LikelyIntendedId}' (edit distance: {ts.EditDistance}). {ts.Reason}"
                }
            });
        }

        foreach (var lc in result.LicenseChanges)
        {
            results.Add(new SarifResult
            {
                RuleId = "NUGETGUARD-LICENSE",
                Level = "warning",
                Message = new SarifMessage
                {
                    Text = $"Package '{lc.PackageId}' ({lc.VersionInstalled}) has a license change starting from v{lc.ChangedInVersion}: {lc.NewLicenseSummary}. Review: {lc.SourceUrl}"
                }
            });
        }

        return results;
    }

    private static string MapSeverityToLevel(string severity) =>
        severity.ToUpperInvariant() switch
        {
            "CRITICAL" or "HIGH" => "error",
            "MODERATE" or "MEDIUM" => "warning",
            "LOW" => "note",
            _ => "warning"
        };

    #region SARIF Schema Models

    private class SarifLog
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }
        [JsonPropertyName("version")]
        public string? Version { get; set; }
        [JsonPropertyName("runs")]
        public List<SarifRun>? Runs { get; set; }
    }

    private class SarifRun
    {
        [JsonPropertyName("tool")]
        public SarifTool? Tool { get; set; }
        [JsonPropertyName("results")]
        public List<SarifResult>? Results { get; set; }
    }

    private class SarifTool
    {
        [JsonPropertyName("driver")]
        public SarifDriver? Driver { get; set; }
    }

    private class SarifDriver
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("version")]
        public string? Version { get; set; }
        [JsonPropertyName("informationUri")]
        public string? InformationUri { get; set; }
        [JsonPropertyName("rules")]
        public List<SarifRule>? Rules { get; set; }
    }

    private class SarifRule
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("shortDescription")]
        public SarifMessage? ShortDescription { get; set; }
        [JsonPropertyName("helpUri")]
        public string? HelpUri { get; set; }
        [JsonPropertyName("defaultConfiguration")]
        public SarifConfiguration? DefaultConfiguration { get; set; }
    }

    private class SarifConfiguration
    {
        [JsonPropertyName("level")]
        public string? Level { get; set; }
    }

    private class SarifResult
    {
        [JsonPropertyName("ruleId")]
        public string? RuleId { get; set; }
        [JsonPropertyName("level")]
        public string? Level { get; set; }
        [JsonPropertyName("message")]
        public SarifMessage? Message { get; set; }
        [JsonPropertyName("locations")]
        public List<SarifLocation>? Locations { get; set; }
    }

    private class SarifMessage
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class SarifLocation
    {
        [JsonPropertyName("physicalLocation")]
        public SarifPhysicalLocation? PhysicalLocation { get; set; }
    }

    private class SarifPhysicalLocation
    {
        [JsonPropertyName("artifactLocation")]
        public SarifArtifactLocation? ArtifactLocation { get; set; }
    }

    private class SarifArtifactLocation
    {
        [JsonPropertyName("uri")]
        public string? Uri { get; set; }
    }

    #endregion
}
