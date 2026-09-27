using System.Text.Json;
using System.Text.Json.Serialization;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Reporting;

public class JsonReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Write(ScanResult result, TextWriter output)
    {
        var json = JsonSerializer.Serialize(result, SerializerOptions);
        output.Write(json);
    }
}
