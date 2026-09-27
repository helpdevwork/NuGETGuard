using NuGetGuard.Core.Models;
using NuGetGuard.Core.Policy;

namespace NuGetGuard.Core.Tests;

public class PolicyFileTests
{
    [Fact]
    public void DefaultPolicy_HasSaneDefaults()
    {
        var policy = new PolicyFile();

        Assert.Equal("High", policy.FailOn);
        Assert.Equal(Severity.High, policy.FailOnSeverity);
        Assert.True(policy.CheckLicenseChanges);
        Assert.True(policy.CheckLatestVersion);
        Assert.True(policy.AllowTyposquatWarningsOnly);
        Assert.Empty(policy.Ignore);
    }

    [Fact]
    public void ShouldIgnore_ReturnsTrueForIgnoredPackage()
    {
        var policy = new PolicyFile
        {
            Ignore = [new IgnoreEntry { PackageId = "SomeLib", Reason = "false positive" }]
        };

        Assert.True(policy.ShouldIgnore("SomeLib"));
        Assert.True(policy.ShouldIgnore("somelib"));
        Assert.False(policy.ShouldIgnore("OtherLib"));
    }

    [Fact]
    public void LoadFromDirectory_MissingFile_ReturnsDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nugetguard-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var policy = PolicyFile.Load(tempDir);
            Assert.Equal("High", policy.FailOn);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void WriteAndLoad_RoundTrips()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nugetguard-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            PolicyFile.WriteDefault(tempDir);
            var loaded = PolicyFile.Load(tempDir);

            Assert.Equal("High", loaded.FailOn);
            Assert.True(loaded.CheckLicenseChanges);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
