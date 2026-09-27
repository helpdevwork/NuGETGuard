using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Tests;

public class SeverityTests
{
    [Theory]
    [InlineData("Critical", Severity.Critical)]
    [InlineData("CRITICAL", Severity.Critical)]
    [InlineData("High", Severity.High)]
    [InlineData("HIGH", Severity.High)]
    [InlineData("Moderate", Severity.Moderate)]
    [InlineData("MEDIUM", Severity.Moderate)]
    [InlineData("Low", Severity.Low)]
    [InlineData("", Severity.None)]
    [InlineData(null, Severity.None)]
    [InlineData("unknown", Severity.None)]
    public void Parse_MapsCorrectly(string? input, Severity expected)
    {
        Assert.Equal(expected, SeverityExtensions.Parse(input));
    }

    [Fact]
    public void Severity_OrderIsCorrect()
    {
        Assert.True(Severity.Critical > Severity.High);
        Assert.True(Severity.High > Severity.Moderate);
        Assert.True(Severity.Moderate > Severity.Low);
        Assert.True(Severity.Low > Severity.None);
    }
}
