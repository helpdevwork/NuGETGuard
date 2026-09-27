using System.Net;
using System.Text;
using NuGetGuard.Core.Detectors;
using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Tests;

public class LicenseChangeDetectorTests
{
    private static HttpClient CreateMockHttpClient(string packageId, Dictionary<string, string> versionLicenses)
    {
        var handler = new FakeRegistrationHandler(packageId, versionLicenses);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task LicenseChanged_BetweenVersions_Flagged()
    {
        var client = CreateMockHttpClient("TestPackage", new()
        {
            ["1.0.0"] = "MIT",
            ["2.0.0"] = "Apache-2.0"
        });

        var detector = new LicenseChangeDetector(client);
        var packages = new List<PackageReference>
        {
            new("TestPackage", "2.0.0", true, "test.csproj")
        };

        var findings = await detector.DetectAsync(packages);

        Assert.Single(findings);
        Assert.Equal("TestPackage", findings[0].PackageId);
        Assert.Contains("MIT", findings[0].NewLicenseSummary);
        Assert.Contains("Apache-2.0", findings[0].NewLicenseSummary);
    }

    [Fact]
    public async Task LicenseUnchanged_NotFlagged()
    {
        var client = CreateMockHttpClient("StablePackage", new()
        {
            ["1.0.0"] = "MIT",
            ["2.0.0"] = "MIT"
        });

        var detector = new LicenseChangeDetector(client);
        var packages = new List<PackageReference>
        {
            new("StablePackage", "2.0.0", true, "test.csproj")
        };

        var findings = await detector.DetectAsync(packages);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task SingleVersion_NotFlagged()
    {
        var client = CreateMockHttpClient("NewPackage", new()
        {
            ["1.0.0"] = "MIT"
        });

        var detector = new LicenseChangeDetector(client);
        var packages = new List<PackageReference>
        {
            new("NewPackage", "1.0.0", true, "test.csproj")
        };

        var findings = await detector.DetectAsync(packages);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task PackageNotFound_NotFlagged()
    {
        var handler = new FakeRegistrationHandler("OtherPackage", new() { ["1.0.0"] = "MIT" });
        var client = new HttpClient(handler);

        var detector = new LicenseChangeDetector(client);
        var packages = new List<PackageReference>
        {
            new("NonExistent", "1.0.0", true, "test.csproj")
        };

        var findings = await detector.DetectAsync(packages);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task MultiplePackages_OnlyFlagsChanged()
    {
        var handler = new FakeRegistrationHandler(new Dictionary<string, Dictionary<string, string>>
        {
            ["changedpkg"] = new() { ["1.0.0"] = "MIT", ["2.0.0"] = "GPL-3.0" },
            ["stablepkg"] = new() { ["1.0.0"] = "Apache-2.0", ["2.0.0"] = "Apache-2.0" }
        });
        var client = new HttpClient(handler);

        var detector = new LicenseChangeDetector(client);
        var packages = new List<PackageReference>
        {
            new("ChangedPkg", "2.0.0", true, "test.csproj"),
            new("StablePkg", "2.0.0", true, "test.csproj")
        };

        var findings = await detector.DetectAsync(packages);

        Assert.Single(findings);
        Assert.Equal("ChangedPkg", findings[0].PackageId);
    }

    private class FakeRegistrationHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Dictionary<string, string>> _packageData;

        public FakeRegistrationHandler(string packageId, Dictionary<string, string> versionLicenses)
        {
            _packageData = new(StringComparer.OrdinalIgnoreCase)
            {
                [packageId.ToLowerInvariant()] = versionLicenses
            };
        }

        public FakeRegistrationHandler(Dictionary<string, Dictionary<string, string>> packageData)
        {
            _packageData = new(packageData, StringComparer.OrdinalIgnoreCase);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();

            if (url.Contains("index.json") && url.Contains("api.nuget.org/v3/index.json"))
            {
                var serviceIndex = """{"resources":[{"@id":"https://fake-reg.test/v3/","@type":"RegistrationsBaseUrl/3.6.0"}]}""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(serviceIndex, Encoding.UTF8, "application/json")
                });
            }

            foreach (var kvp in _packageData)
            {
                if (url.Contains($"/{kvp.Key}/index.json", StringComparison.OrdinalIgnoreCase))
                {
                    var items = string.Join(",", kvp.Value.Select(vl =>
                        "{\"catalogEntry\":{\"version\":\"" + vl.Key + "\",\"licenseExpression\":\"" + vl.Value + "\"}}"));

                    var json = "{\"items\":[{\"items\":[" + items + "]}]}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
