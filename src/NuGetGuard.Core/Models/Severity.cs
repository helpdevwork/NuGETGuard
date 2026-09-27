namespace NuGetGuard.Core.Models;

public enum Severity
{
    None = 0,
    Low = 1,
    Moderate = 2,
    High = 3,
    Critical = 4
}

public static class SeverityExtensions
{
    public static Severity Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Severity.None;

        return value.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => Severity.Critical,
            "HIGH" => Severity.High,
            "MODERATE" or "MEDIUM" => Severity.Moderate,
            "LOW" => Severity.Low,
            _ => Severity.None
        };
    }
}
