# CI/CD Integration

Run NuGetGuard as a dedicated pipeline step to catch dependency risks on every pull request, before they merge — independent of whether you're also using the [MSBuild task](02-MSBuild-Task-Integration.md) for local/build-time checks.

## GitHub Actions

### Using the NuGetGuard Action (recommended)

NuGetGuard ships as a reusable [GitHub Action](https://github.com/marketplace/actions/nugetguard), so you don't need to manually install the CLI or wire up SARIF upload yourself:

```yaml
permissions:
  contents: read
  security-events: write

jobs:
  nugetguard:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - uses: helpdevwork/NuGETGuard@v0.3.0
        id: nugetguard
        with:
          project-path: "."
          fail-on: "High"
      - uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: ${{ steps.nugetguard.outputs.report-path }}
```

| Input | Default | Description |
|---|---|---|
| `project-path` | `.` | Directory to scan |
| `format` | `sarif` | `console`, `json`, or `sarif` |
| `output` | `nugetguard-results.sarif` | Report file path |
| `fail-on` | `High` | Severity threshold that fails the step |
| `no-typosquat` | `false` | Skip typosquat detection |
| `no-license-check` | `false` | Skip license change detection |
| `no-version-check` | `false` | Skip outdated version detection |

`if: always()` on the SARIF upload step ensures findings still get uploaded even when the scan step fails the job (since a High+ finding causes a non-zero exit code by default).

### Manual CLI install (alternative)

### Basic scan (fail the build on High+ vulnerabilities)

```yaml
name: Dependency Security Scan

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  nugetguard:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - run: dotnet tool install --global NuGetGuard.Cli
      - run: nugetguard scan
```

The step fails (non-zero exit code) if any vulnerability at or above the configured threshold is found, failing the workflow.

### With SARIF upload for PR annotations

This surfaces findings directly as inline annotations on the "Files changed" tab of a pull request, using GitHub's built-in code scanning UI.

```yaml
permissions:
  contents: read
  security-events: write

jobs:
  nugetguard:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - run: dotnet tool install --global NuGetGuard.Cli
      - run: nugetguard scan --format sarif --output results.sarif
        continue-on-error: true
      - uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: results.sarif
```

`continue-on-error: true` lets the SARIF upload happen even if the scan step itself would otherwise fail the job — the code scanning alerts become the source of truth instead of the raw exit code. Remove it if you want the workflow to fail immediately on findings.

### With a custom fail threshold

```yaml
- run: nugetguard scan --fail-on Critical
```

## Azure DevOps

```yaml
trigger:
  - main

pool:
  vmImage: "ubuntu-latest"

steps:
  - task: UseDotNet@2
    inputs:
      version: "10.0.x"
  - script: dotnet tool install --global NuGetGuard.Cli
    displayName: "Install NuGetGuard"
  - script: nugetguard scan --format json --output $(Build.ArtifactStagingDirectory)/nugetguard-report.json
    displayName: "Scan dependencies"
  - task: PublishBuildArtifacts@1
    inputs:
      pathToPublish: "$(Build.ArtifactStagingDirectory)/nugetguard-report.json"
      artifactName: "nugetguard-report"
```

## GitLab CI

```yaml
nugetguard:
  image: mcr.microsoft.com/dotnet/sdk:10.0
  stage: test
  script:
    - dotnet tool install --global NuGetGuard.Cli
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - nugetguard scan --format json --output nugetguard-report.json
  artifacts:
    when: always
    paths:
      - nugetguard-report.json
```

## Using a Policy File in CI

Commit a `.nugetguard.json` to your repository root (see [Installation & CLI Usage](01-Installation-and-CLI-Usage.md#policy-file-nugetguardjson)) so every environment — local dev, MSBuild, and CI — shares the same fail threshold and ignore list. CI steps then just need:

```bash
nugetguard scan
```

No flags required; the policy file is picked up automatically from the scanned directory.

## Caching Considerations

NuGetGuard queries three external services on every scan: OSV.dev (vulnerabilities), and the NuGet V3 API twice (registration metadata for license checks, and the flat container for version checks). On a large dependency tree, CI runners with restricted network egress may need those hosts allow-listed:

- `api.osv.dev`
- `api.nuget.org`

## Recommended Pattern

For most teams, the effective setup is:

1. **MSBuild task** on your main library/service projects — catches issues at build time, in every developer's inner loop, before code is even pushed.
2. **CLI step in CI** with SARIF upload — catches anything missed locally and surfaces it as PR annotations for reviewers.
3. **Shared `.nugetguard.json`** policy file — keeps both consistent and lets you tune the fail threshold or ignore known false positives in one place.
