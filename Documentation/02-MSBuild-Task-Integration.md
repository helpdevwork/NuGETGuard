# MSBuild Task Integration

`NuGetGuard.MSBuildTask` runs a security scan automatically every time you build or restore a project — no separate CLI install or CI step required. Findings appear directly in your build output (and in your IDE's Error List).

## Install

Add the package reference to any project you want scanned:

```xml
<ItemGroup>
  <PackageReference Include="NuGetGuard.MSBuildTask" Version="0.3.0" PrivateAssets="all" />
</ItemGroup>
```

`PrivateAssets="all"` ensures the task itself doesn't get published or flow to consumers of your library — it's a build-time-only dependency.

## How It Works

The package ships an MSBuild `.targets` file that hooks into the `Restore` target:

```xml
<Target Name="NuGetGuardScan" AfterTargets="Restore" Condition="'$(NuGetGuardEnabled)' != 'false'">
  <NuGetGuardBuildTask
      ProjectPath="$(MSBuildProjectFullPath)"
      FailOnSeverity="$(NuGetGuardFailOn)"
      ContinueOnError="$(NuGetGuardContinueOnError)" />
</Target>
```

Every time `dotnet restore` (or `dotnet build`, which restores implicitly) runs, NuGetGuard scans your resolved dependencies and reports:

- Vulnerabilities as MSBuild **errors** (if at or above the fail threshold) or **warnings** (below threshold)
- Typosquat findings as **warnings**
- License changes as **warnings**

No configuration is required for this to work out of the box.

## Configuration via MSBuild Properties

Set these in your `.csproj` or a shared `Directory.Build.props` to customize behavior:

```xml
<PropertyGroup>
  <!-- Disable the scan entirely for this project -->
  <NuGetGuardEnabled>false</NuGetGuardEnabled>

  <!-- Severity that causes the build to fail (default: High) -->
  <NuGetGuardFailOn>Critical</NuGetGuardFailOn>

  <!-- Report findings without failing the build -->
  <NuGetGuardContinueOnError>true</NuGetGuardContinueOnError>
</PropertyGroup>
```

| Property | Default | Effect |
|---|---|---|
| `NuGetGuardEnabled` | `true` | Set to `false` to skip the scan for a project |
| `NuGetGuardFailOn` | `High` | Minimum vulnerability severity that fails the build (`None`, `Low`, `Moderate`, `High`, `Critical`) |
| `NuGetGuardContinueOnError` | `false` | When `true`, scan errors are logged but the build continues regardless of findings |

## Applying to an Entire Solution

Rather than adding the package reference to every project individually, add it once in a `Directory.Build.props` at your solution root:

```xml
<Project>
  <ItemGroup>
    <PackageReference Include="NuGetGuard.MSBuildTask" Version="0.3.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Every project under that directory tree picks up the scan automatically.

## Disabling for Specific Projects

If most of your solution should be scanned but a few projects (e.g. test projects, generated code) should be skipped:

```xml
<PropertyGroup>
  <NuGetGuardEnabled>false</NuGetGuardEnabled>
</PropertyGroup>
```

## Relationship to the CLI Tool

The MSBuild task and the `nugetguard` CLI share the same underlying scan engine (`NuGetGuard.Core`) and perform identical checks — vulnerabilities, typosquats, license changes, and outdated versions. The differences:

| | MSBuild Task | CLI |
|---|---|---|
| Trigger | Automatic, on every restore/build | Manual, or scripted in CI |
| Output | MSBuild errors/warnings | Console, JSON, or SARIF |
| Policy file (`.nugetguard.json`) | Not read — configure via MSBuild properties instead | Fully supported |
| Version/latest-version check | Skipped (kept fast for inner-loop builds) | Included by default |

For SARIF output, CI annotations, or full policy-file control, use the [CLI](01-Installation-and-CLI-Usage.md) in your pipeline alongside or instead of the build task.
