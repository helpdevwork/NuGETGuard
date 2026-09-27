# NuGetGuard

Catch known vulnerabilities, typosquats, and license-change surprises in your NuGet dependencies — automatically, on every build.

[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## What it checks

- **Known vulnerabilities** — queries the [OSV.dev](https://osv.dev) database for CVEs affecting your exact package versions
- **Typosquatted package names** — flags packages whose names are suspiciously close to popular NuGet packages
- **License changes** — warns when a package you depend on has switched to a commercial license (AutoMapper, MediatR, EPPlus, etc.)
- **Outdated versions** — shows latest available version and what changed

## Install

```bash
dotnet tool install --global NuGetGuard.Cli
```

## Use

```bash
# Scan current directory
nugetguard scan

# Scan a specific project
nugetguard scan ./src/MyProject

# Output as JSON or SARIF (for CI annotations)
nugetguard scan --format json
nugetguard scan --format sarif --output results.sarif

# Skip specific checks
nugetguard scan --no-typosquat --no-license-check

# Fail only on Critical vulnerabilities
nugetguard scan --fail-on Critical
```

## MSBuild Integration

Add the MSBuild task package to scan automatically on every build:

```xml
<PackageReference Include="NuGetGuard.MSBuildTask" Version="0.1.0" PrivateAssets="all" />
```

No configuration needed — it runs after `dotnet restore` with sane defaults.

## Policy File

Create a `.nugetguard.json` at your repo root to customize behavior:

```bash
nugetguard init
```

```json
{
  "failOn": "High",
  "ignore": [
    { "packageId": "SomeLibrary", "reason": "false positive, tracked in JIRA-123" }
  ],
  "allowTyposquatWarningsOnly": true,
  "checkLicenseChanges": true,
  "checkLatestVersion": true
}
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Clean or below threshold |
| 1 | Findings at or above `failOn` severity |
| 2 | Tool error (network, parse failure) |

## CI/CD Usage

### GitHub Actions

```yaml
steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: "9.0.x"
  - run: dotnet tool install --global NuGetGuard.Cli
  - run: nugetguard scan --format sarif --output results.sarif
  - uses: github/codeql-action/upload-sarif@v3
    with:
      sarif_file: results.sarif
```

## Contributing

Contributions to the **typosquat list** (`data/popular-packages.json`) and **license-change list** (`data/license-changed.json`) are especially welcome — these curated datasets are what make NuGetGuard uniquely useful.

## License

MIT — free forever for individual and commercial use. See [LICENSE](LICENSE).
