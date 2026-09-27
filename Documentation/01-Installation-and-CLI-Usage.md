# Installation & CLI Usage

NuGetGuard is a .NET global tool that scans your project's NuGet dependencies for known vulnerabilities, typosquatted package names, license changes, and outdated versions.

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later installed
- Internet access (the scanner queries OSV.dev and api.nuget.org)

## Install

```bash
dotnet tool install --global NuGetGuard.Cli
```

This installs the `nugetguard` command globally, available from any directory.

To update to the latest version later:

```bash
dotnet tool update --global NuGetGuard.Cli
```

To check your installed version:

```bash
nugetguard --version
```

## Basic Usage

```bash
# Scan the current directory
nugetguard scan

# Scan a specific project or solution directory
nugetguard scan ./src/MyProject
```

NuGetGuard automatically detects how your project manages packages:

- If a `packages.lock.json` file exists, it resolves exact pinned versions (including transitive dependencies) from the lock file.
- Otherwise, it parses your `.csproj` files and `Directory.Packages.props` (central package management) directly.

## Command Reference

```
nugetguard scan [path]          Scan a project/solution directory (default: current directory)
  --format console|json|sarif   Output format (default: console)
  --output <file>                Write report to file instead of stdout
  --fail-on <severity>           Override policy severity threshold (None|Low|Moderate|High|Critical)
  --no-typosquat                  Skip typosquat detection
  --no-license-check              Skip license change detection
  --no-version-check              Skip latest version check

nugetguard init                 Create a default .nugetguard.json policy file
nugetguard --version             Show installed version
nugetguard --help                Show help
```

## Output Formats

### Console (default)

Human-readable ASCII tables, grouped by finding type — ideal for local development.

```bash
nugetguard scan
```

### JSON

Machine-readable output for scripting or feeding into other tools.

```bash
nugetguard scan --format json --output report.json
```

### SARIF

[SARIF 2.1.0](https://sarifweb.azurewebsites.net/) format, designed to be consumed by code-scanning tools such as GitHub's `codeql-action/upload-sarif`, so findings show up as annotations directly on your pull requests.

```bash
nugetguard scan --format sarif --output results.sarif
```

## What Each Check Does

| Check | Source | Description |
|---|---|---|
| **Vulnerabilities** | [OSV.dev](https://osv.dev) | Queries the OSV batch API for every resolved package + version, mapping CVSS scores to severity levels (Low/Moderate/High/Critical) |
| **Typosquats** | Embedded dataset (top 1,200 NuGet packages by downloads) | Flags package names within 2 edit operations (Damerau-Levenshtein distance) of a popular package name — e.g. `Newtonsoft.Jsn` instead of `Newtonsoft.Json` |
| **License changes** | Live NuGet Registration API | Compares the license (SPDX expression or license URL) of your installed version against the immediately preceding version, flagging any change |
| **Version info** | Live NuGet V3 API | Reports whether each package is on the latest stable version |

## Disabling Individual Checks

```bash
nugetguard scan --no-typosquat --no-license-check
nugetguard scan --no-version-check
```

## Controlling the Fail Threshold

By default, NuGetGuard exits with code `1` if any vulnerability at or above **High** severity is found (configurable via policy file — see the ignore/threshold options below). You can override this per-run:

```bash
# Only fail the build on Critical vulnerabilities
nugetguard scan --fail-on Critical

# Never fail (report only)
nugetguard scan --fail-on None
```

## Exit Codes

| Code | Meaning |
|---|---|
| `0` | Clean scan, or all findings below the fail threshold |
| `1` | One or more findings at or above the `failOn` severity |
| `2` | Tool error (network failure, unreadable project, invalid arguments) |

Use the exit code to gate CI pipelines or pre-commit hooks — see [CI/CD Integration](03-CICD-Integration.md) for examples.

## Policy File (`.nugetguard.json`)

Create a policy file at your repository root to customize default behavior without passing flags every time:

```bash
nugetguard init
```

This creates:

```json
{
  "failOn": "High",
  "ignore": [],
  "allowTyposquatWarningsOnly": true,
  "checkLicenseChanges": true,
  "checkLatestVersion": true
}
```

| Field | Type | Description |
|---|---|---|
| `failOn` | string | Severity threshold that causes a non-zero exit code: `None`, `Low`, `Moderate`, `High`, `Critical` |
| `ignore` | array | List of `{ "packageId": "...", "reason": "..." }` entries to exclude specific packages from all checks (e.g. known false positives) |
| `allowTyposquatWarningsOnly` | bool | When `true`, typosquat findings are reported but never cause a build failure on their own |
| `checkLicenseChanges` | bool | Enable/disable license-change detection by default |
| `checkLatestVersion` | bool | Enable/disable outdated-version detection by default |

Example with an ignored package:

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

Flags passed on the command line (like `--fail-on`) always override the policy file.
