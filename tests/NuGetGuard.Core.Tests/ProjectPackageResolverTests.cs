using NuGetGuard.Core.Resolvers;

namespace NuGetGuard.Core.Tests;

public class ProjectPackageResolverTests
{
    [Fact]
    public async Task ParsesCsproj_ExtractsPackageReferences()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nugetguard-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var csproj = Path.Combine(tempDir, "Test.csproj");
            File.WriteAllText(csproj, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                    <PackageReference Include="Serilog" Version="3.1.1" />
                  </ItemGroup>
                </Project>
                """);

            var resolver = new ProjectPackageResolver();
            var packages = await resolver.ResolveAsync(tempDir);

            Assert.Equal(2, packages.Count);
            Assert.Contains(packages, p => p.Id == "Newtonsoft.Json" && p.ResolvedVersion == "13.0.3");
            Assert.Contains(packages, p => p.Id == "Serilog" && p.ResolvedVersion == "3.1.1");
            Assert.All(packages, p => Assert.True(p.IsDirect));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ParsesCsproj_WithCentralPackageManagement()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nugetguard-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Directory.Packages.props"), """
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                  </ItemGroup>
                </Project>
                """);

            File.WriteAllText(Path.Combine(tempDir, "Test.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """);

            var resolver = new ProjectPackageResolver();
            var packages = await resolver.ResolveAsync(tempDir);

            Assert.Single(packages);
            Assert.Equal("Newtonsoft.Json", packages[0].Id);
            Assert.Equal("13.0.3", packages[0].ResolvedVersion);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
