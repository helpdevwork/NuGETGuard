# NuGetGuard

Catch known vulnerabilities, typosquats, and license-change surprises in your NuGet dependencies — automatically, on every build.

[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## What it checks

- **Known vulnerabilities** — queries the [OSV.dev](https://osv.dev) database for CVEs affecting your exact package versions
- **Typosquatted package names** — flags packages whose names are suspiciously close to the top 1,200 most-downloaded NuGet packages
- **License changes** — dynamically compares license metadata between the installed version and prior versions via the NuGet API, so it works for any package, not just a curated list
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
<PackageReference Include="NuGetGuard.MSBuildTask" Version="0.3.0" PrivateAssets="all" />
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
      dotnet-version: "10.0.x"
  - run: dotnet tool install --global NuGetGuard.Cli
  - run: nugetguard scan --format sarif --output results.sarif
  - uses: github/codeql-action/upload-sarif@v3
    with:
      sarif_file: results.sarif
```

## Contributing

Contributions to the **typosquat list** (`data/popular-packages.json`) are especially welcome — keeping it current is what makes typosquat detection effective.

## Documentation

See the [Documentation](Documentation/) folder for detailed guides:

- [Installation & CLI Usage](Documentation/01-Installation-and-CLI-Usage.md)
- [MSBuild Task Integration](Documentation/02-MSBuild-Task-Integration.md)
- [CI/CD Integration](Documentation/03-CICD-Integration.md)

## License

MIT — free forever for individual and commercial use. See [LICENSE](LICENSE).
