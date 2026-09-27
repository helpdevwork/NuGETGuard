# NuGetGuard — Low-Level Design (LLD)

**Companion to:** 01-High-Level-Design.md
**Purpose:** Enough implementation detail — project structure, module contracts, API payloads, schemas, and CLI spec — for Claude Code to scaffold and build the MVP directly.

---

## 1. Solution / Project Structure

```
NuGetGuard/
├── NuGetGuard.sln
├── src/
│   ├── NuGetGuard.Core/                # class library, all business logic
│   │   ├── Resolvers/
│   │   │   ├── ProjectPackageResolver.cs
│   │   │   └── LockFilePackageResolver.cs
│   │   ├── Detectors/
│   │   │   ├── VulnerabilityScanner.cs
│   │   │   ├── TyposquatDetector.cs
│   │   │   └── LicenseChangeDetector.cs
│   │   ├── VersionInfo/
│   │   │   └── VersionInfoService.cs
│   │   ├── Reporting/
│   │   │   ├── ConsoleReportWriter.cs
│   │   │   ├── JsonReportWriter.cs
│   │   │   └── SarifReportWriter.cs
│   │   ├── Caching/
│   │   │   └── SqliteResponseCache.cs
│   │   ├── Policy/
│   │   │   └── PolicyFile.cs           # .nugetguard.json model + loader
│   │   ├── Models/
│   │   │   ├── PackageReference.cs
│   │   │   ├── ScanResult.cs
│   │   │   ├── VulnerabilityFinding.cs
│   │   │   ├── TyposquatFinding.cs
│   │   │   ├── LicenseFinding.cs
│   │   │   └── VersionFinding.cs
│   │   └── ScanEngine.cs               # orchestrator, single entry point
│   │
│   ├── NuGetGuard.Cli/                 # `dotnet tool`, references Core
│   │   └── Program.cs
│   │
│   └── NuGetGuard.MSBuildTask/         # MSBuild Task package, references Core
│       ├── NuGetGuardBuildTask.cs
│       └── build/NuGetGuard.targets
│
├── data/
│   ├── popular-packages.json           # top ~500 NuGet package names, for typosquat distance checks
│   └── license-changed.json            # curated list: package, version-from, new-license, source-url
│
├── tests/
│   ├── NuGetGuard.Core.Tests/
│   └── NuGetGuard.Cli.Tests/
│
└── README.md
```

**Why split Core / Cli / MSBuildTask:** the Free tier ships two consumption modes (explicit `nugetguard scan` command, and automatic on `dotnet build`) that must share 100% of the detection logic. Only `Core` talks to external APIs; `Cli` and `MSBuildTask` are thin front-ends.

---

## 2. Core Data Models

```csharp
public record PackageReference(
    string Id,               // e.g. "Newtonsoft.Json"
    string ResolvedVersion,  // e.g. "12.0.1"
    bool IsDirect,           // top-level vs transitive
    string SourceFile        // which .csproj / lock file it came from
);

public record VulnerabilityFinding(
    string PackageId,
    string Version,
    string OsvId,            // e.g. "GHSA-5crp-9r3c-p9vr"
    string Severity,         // Critical/High/Moderate/Low, mapped from OSV/CVSS
    string Summary,
    string? FixedVersion,
    string DetailsUrl
);

public record TyposquatFinding(
    string PackageId,
    string LikelyIntendedId,
    double EditDistance,
    string Reason
);

public record LicenseFinding(
    string PackageId,
    string VersionInstalled,
    string ChangedInVersion,
    string NewLicenseSummary,
    string SourceUrl
);

public record VersionFinding(
    string PackageId,
    string CurrentVersion,
    string LatestVersion,
    bool IsUpToDate,
    string? ChangeSummary      // short "what changed" text, optional
);

public record ScanResult(
    IReadOnlyList<PackageReference> Packages,
    IReadOnlyList<VulnerabilityFinding> Vulnerabilities,
    IReadOnlyList<TyposquatFinding> Typosquats,
    IReadOnlyList<LicenseFinding> LicenseChanges,
    IReadOnlyList<VersionFinding> VersionInfo,
    DateTimeOffset ScannedAt
);
```

---

## 3. Module Detail

### 3.1 PackageResolver

**Input:** path to a project directory.
**Output:** `IReadOnlyList<PackageReference>`.

Implementation order of preference:
1. If `packages.lock.json` exists (recommended — ask users to enable `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` for full-fidelity resolved versions including transitive packages), parse it directly — it's already JSON with exact resolved versions.
2. Else, parse `.csproj` / `Directory.Packages.props` for `<PackageReference>` entries (direct only — no transitive resolution without invoking `dotnet restore`).
3. For the MVP, direct-only resolution is acceptable; lock-file support is what unlocks transitive coverage and should be flagged clearly in output ("N transitive dependencies not scanned — enable lock file for full coverage").

### 3.2 VulnerabilityScanner

Uses the **OSV.dev batch API**.

```
POST https://api.osv.dev/v1/querybatch
Content-Type: application/json

{
  "queries": [
    { "package": { "ecosystem": "NuGet", "name": "Newtonsoft.Json" }, "version": "12.0.1" },
    { "package": { "ecosystem": "NuGet", "name": "System.Text.Json" }, "version": "4.6.0" }
  ]
}
```

- `querybatch` returns only vulnerability **IDs** + modified timestamps per query, in the same order as input (up to 1000 queries per call).
- For each returned ID, resolve full details via:

```
POST https://api.osv.dev/v1/query
{ "package": { "ecosystem": "NuGet", "name": "Newtonsoft.Json" }, "version": "12.0.1" }
```
  — or batch a follow-up call per unique ID if the API surface used exposes a get-by-id path; if not available, the single `/v1/query` per affected package (not per package overall — only for packages that had hits) is acceptable since hits are the minority case.
- Cache full records locally (see §7) keyed by `osv_id` since vulnerability records change far less often than the packages themselves.
- Map OSV `severity` (CVSS vector, when present) to a simple `Critical/High/Moderate/Low` bucket for FR-7 exit-code logic. If no CVSS is present, fall back to OSV's `database_specific.severity` string when the source (e.g. GitHub Advisory) provides one.

### 3.3 TyposquatDetector

- Load `data/popular-packages.json` at startup (bundled with the tool, ~500 entries: package id + weekly download count, refreshed periodically as a release asset — not fetched live in MVP).
- For every scanned package **not** already in that list, compute normalized edit distance (Damerau-Levenshtein) against every popular package name.
- Flag when: distance ≤ 2 **and** the scanned package has meaningfully lower downloads/age than the popular package it resembles (use NuGet search API `totalDownloads` as a cheap heuristic — see §3.4).
- This is intentionally a heuristic, not a certainty — report as "possible typosquat, review manually," never as a hard block by default.

### 3.4 VersionInfoService

Uses the **NuGet V3 API**.

1. Fetch the service index once per run (cache for the process lifetime):
   `GET https://api.nuget.org/v3/index.json` → locate `resources[@type="PackageBaseAddress/3.0.0"].@id` and `resources[@type="SearchQueryService"].@id`.
2. Get all versions of a package (cheap, flat list):
   `GET {PackageBaseAddress}/{lower_id}/index.json`
   → response: `{ "versions": ["9.0.1", "10.0.3", ... "13.0.3"] }`
   → latest = highest valid SemVer in the list (exclude prerelease unless the installed version is itself prerelease).
3. For richer metadata (downloads, description, license URL — used by the typosquat heuristic and the report), optionally call:
   `GET {SearchQueryService}?q=packageid:{id}&prerelease=false`
4. **"What changed" summary (MVP-simple approach):** pull the package's `ProjectUrl`/`RepositoryUrl` from the registration leaf; if it's a GitHub URL, fetch that repo's latest release notes via the public GitHub REST API (`/repos/{owner}/{repo}/releases/tags/{version}` or `/latest`) and surface the first ~200 characters as the change summary. If no repository URL or no matching release exists, omit the summary rather than guessing — never fabricate changelog content.

### 3.5 LicenseChangeDetector

- Load `data/license-changed.json`, a small, hand-maintained list, schema:

```json
[
  {
    "packageId": "AutoMapper",
    "changedInVersion": "13.0.0",
    "previousLicense": "MIT",
    "newLicense": "Commercial (Eyeconic/Meziantou dual license)",
    "sourceUrl": "https://github.com/AutoMapper/AutoMapper/blob/main/LICENSE.txt",
    "notes": "Free for individuals/small teams under a defined revenue threshold; commercial license required above it."
  },
  {
    "packageId": "MediatR",
    "changedInVersion": "13.0.0",
    "previousLicense": "Apache-2.0",
    "newLicense": "Commercial",
    "sourceUrl": "https://github.com/jbogard/MediatR",
    "notes": "See publisher licensing page for current terms before relying on this summary."
  }
]
```
- For each scanned package present in this list, compare resolved version against `changedInVersion` (SemVer comparison) and flag if installed version is at or above it.
- **This list is the product's actual moat** — treat it as a first-class, versioned data asset (own file, own changelog, contributions welcome via PR) rather than an inline constant. Always link to the publisher's own licensing page rather than asserting exact commercial terms, since those change and carry real legal weight — the tool's job is to prompt a human review, not to give legal advice.

### 3.6 ReportGenerator

Three writers implementing a common `IReportWriter.Write(ScanResult result, TextWriter output)`:

- **Console:** grouped table — Vulnerabilities (sorted by severity desc) → Possible Typosquats → License Changes → Version/Update summary. Use simple ASCII, no color dependency required (add color as a nice-to-have via `Spectre.Console` if desired).
- **JSON:** serialize `ScanResult` directly (camelCase, `System.Text.Json`) — this is the machine-readable contract other tools (including a future Pro dashboard) consume.
- **SARIF:** map each `VulnerabilityFinding` to a SARIF `result` object so GitHub/Azure DevOps can annotate the PR diff directly. This is what makes the free tier valuable inside CI without needing our own backend.

### 3.7 Policy File (`.nugetguard.json`)

Lives at the repo root, optional (sane defaults if absent):

```json
{
  "failOn": "High",                 // None | Low | Moderate | High | Critical
  "ignore": [
    { "packageId": "SomeLibrary", "reason": "false positive, tracked in JIRA-123" }
  ],
  "allowTyposquatWarningsOnly": true,
  "checkLicenseChanges": true,
  "checkLatestVersion": true
}
```

### 3.8 Caching (`SqliteResponseCache`)

- Single local SQLite file (e.g. `%LOCALAPPDATA%/nugetguard/cache.db` or `~/.cache/nugetguard/cache.db`).
- Two tables: `osv_cache(key, response_json, cached_at)`, `nuget_cache(key, response_json, cached_at)`.
- TTL: 6 hours for vulnerability data, 24 hours for version/metadata (configurable).
- Purpose: keep repeated local builds fast and avoid rate-limit issues on shared/CI machines running many builds per day.

---

## 4. CLI Specification

```
nugetguard scan [path]                  # scan a project/solution directory (default: cwd)
    --format console|json|sarif         # default: console
    --output <file>                     # write report to file instead of stdout
    --fail-on <severity>                # override policy file
    --no-typosquat
    --no-license-check
    --no-version-check
    --offline                           # use cache only, never hit network (fails gracefully if cache empty)

nugetguard init                         # scaffolds a default .nugetguard.json

nugetguard --version
nugetguard --help
```

Exit codes: `0` = clean or below threshold, `1` = findings at/above `failOn` threshold, `2` = tool error (network, parse failure, etc.) — distinguishing "found a real problem" from "the tool itself broke" matters for CI scripting.

---

## 5. MSBuild Integration

Package `NuGetGuard.MSBuildTask` ships a `build/NuGetGuard.targets` that hooks in after restore:

```xml
<Project>
  <Target Name="NuGetGuardScan" AfterTargets="Restore" Condition="'$(NuGetGuardEnabled)' != 'false'">
    <NuGetGuardBuildTask
        ProjectPath="$(MSBuildProjectFullPath)"
        FailOnSeverity="$(NuGetGuardFailOn)"
        ContinueOnError="$(NuGetGuardContinueOnError)" />
  </Target>
</Project>
```

Consumers opt in with a single `<PackageReference Include="NuGetGuard.MSBuildTask" Version="x.y.z" PrivateAssets="all" />` — mirrors exactly how a developer would "just install Newtonsoft" as referenced in product discussions: one line, zero configuration required for sane defaults.

---

## 6. GitHub Action Wrapper (v0.2, described here for continuity)

```yaml
# action.yml (published separately as nugetguard/scan-action)
runs:
  using: "composite"
  steps:
    - run: dotnet tool install --global NuGetGuard.Cli
      shell: bash
    - run: nugetguard scan --format sarif --output results.sarif
      shell: bash
    - uses: github/codeql-action/upload-sarif@v3
      with:
        sarif_file: results.sarif
```

---

## 7. Pro Tier Backend (design only, not MVP build target)

- Minimal API (ASP.NET Core minimal API or Azure Functions) with three responsibilities: **(1)** validate license keys (`Standard.Licensing` or similar, offline-verifiable signed keys so the CLI can check without a network call every run), **(2)** receive scan results from CI runs and store the latest per-repo snapshot, **(3)** dispatch webhook alerts to Slack/Teams when a new finding appears versus the last stored snapshot.
- Storage: a single small relational table (`ScanSnapshots: OrgId, RepoId, ScanResultJson, ScannedAt`) — no need for anything heavier at low scale; a serverless/consumption-billed DB (e.g. a small managed Postgres or a pay-as-you-go serverless SQL tier) keeps idle cost near zero.
- This backend is **stateless per request** and low-traffic (one write per CI run, not per developer keystroke), which is why it stays cheap even at a few hundred developers — see HLD §4 cost note.

---

## 8. Testing Strategy

- **Unit tests** for each detector using recorded/mocked OSV and NuGet API responses (no live network calls in CI test runs).
- **Golden-file tests** for each `IReportWriter` (console/JSON/SARIF) against a fixed `ScanResult` fixture, to catch accidental format regressions.
- **Integration test** (separate, opt-in CI job) that runs a real scan against a small fixture project with 2–3 packages known to have historical CVEs (e.g. an old `Newtonsoft.Json` or `System.Text.Json` version), to validate the live API integration periodically without slowing the main test suite.

---

## 9. Build Order for Claude Code (suggested implementation sequence)

1. `NuGetGuard.Core/Models` — data records (no logic, fast to scaffold, unblocks everything else).
2. `PackageResolver` (csproj-only path first; lock-file path second).
3. `VersionInfoService` (simplest external integration — flat version list, no auth).
4. `VulnerabilityScanner` (OSV batch + detail calls).
5. `TyposquatDetector` + `data/popular-packages.json` seed file.
6. `LicenseChangeDetector` + `data/license-changed.json` seed file.
7. `ReportGenerator` — console writer first (fastest feedback loop while building), then JSON, then SARIF.
8. `SqliteResponseCache` — wire in once the above work uncached, to avoid premature complexity.
9. `NuGetGuard.Cli` — thin wrapper over `Core`, implements the CLI spec in §4.
10. `NuGetGuard.MSBuildTask` — thin wrapper over `Core`, implements §5.
11. Tests throughout, not bolted on at the end — at minimum, unit tests per detector as it's built (step-by-step, matching §8).

This order front-loads everything needed to get a working, useful `nugetguard scan` on the command line as early as possible, with the MSBuild/CI integration layered on top of already-proven logic.
