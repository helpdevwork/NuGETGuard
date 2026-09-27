# NuGetGuard — High-Level Design (HLD)

**Working name:** NuGetGuard
**Version:** 0.1 (MVP scope)
**Audience:** Claude Code / implementing engineers
**Purpose of this document:** Define what the product is, why it exists, its architecture, and what "done" looks like for the MVP, so implementation can begin without ambiguity.

---

## 1. Problem Statement

When a .NET developer (or an AI coding assistant acting on their behalf, e.g. GitHub Copilot, Claude Code) adds a NuGet package to a project, nothing in the default workflow tells them:

1. Whether that package version has a known security vulnerability.
2. Whether the package name is a typosquat of a popular package (e.g. `Newtonsoft.Json` vs `Newtonsoft.Json.Fake`).
3. Whether the package's license recently changed to a paid/commercial license (e.g. AutoMapper, MediatR, MassTransit).
4. What the latest available version is, and what changed since the version being installed.

Enterprises currently only catch these issues via periodic (often quarterly) manual security scans — long after the risky package is already in production. NuGetGuard closes that gap by moving the check to **install time** and **build time**, and by making it a permanent, automatic part of the developer's daily workflow rather than a point-in-time audit.

## 2. Goals

- Detect known vulnerabilities in a package + version at the moment it's added or built.
- Detect typosquatted / suspicious package names.
- Detect license changes on tracked packages (curated list, MVP) with a path to a fuller dataset later.
- Surface the latest available version and a short summary of what changed, even when there are no security issues — this is what makes the tool useful *every single day*, not just when something is wrong.
- Ship as a free, MIT-licensed CLI + MSBuild integration that individual developers install as easily as any other NuGet package.
- Design the paid tier (continuous CI/CD monitoring, alerts, team policy) as a natural upgrade path from day one, without over-building it in the MVP.

## 3. Non-Goals (MVP)

- We are **not** building our own vulnerability database. We consume existing public sources (OSV.dev, GitHub Advisory Database).
- We are **not** building a hosted SaaS dashboard in the MVP. MVP is local-first (CLI + build integration). The paid, always-on monitoring service is a phase 2 deliverable, described but not built here.
- We are **not** attempting to patch or auto-upgrade vulnerable packages automatically in v0.1 — we report and recommend, we don't mutate the project file yet.

## 4. Product Shape

Two tiers, one codebase:

| Tier | What it does | Where it runs | Cost to us |
|---|---|---|---|
| **Free** | On-demand CLI scan + MSBuild task that runs on every local build | Entirely on the developer's machine | $0 — no server involved |
| **Pro (paid)** | Free tier features **plus** continuous CI/CD monitoring, Slack/Teams alerts on new PRs, custom org policy rules, SBOM export, license-key gated | CLI/task runs locally or in CI; a lightweight cloud backend handles alerting, policy sync, and license validation | Small — cloud backend is stateless/low-traffic, see LLD §8 |

## 5. High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Developer / CI Machine                       │
│                                                                        │
│  dotnet build / dotnet restore                                       │
│        │                                                              │
│        ▼                                                              │
│  ┌───────────────────┐                                               │
│  │  NuGetGuard.Task    │  (MSBuild task, hooks post-restore)          │
│  │  NuGetGuard.Cli     │  (standalone `nugetguard scan` command)      │
│  └─────────┬──────────┘                                               │
│            │ resolves package list from project/lock file            │
│            ▼                                                          │
│  ┌────────────────────────┐                                          │
│  │  NuGetGuard.Core         │  orchestrates the scan                 │
│  │  ├─ PackageResolver       │  reads csproj / packages.lock.json     │
│  │  ├─ VulnerabilityScanner  │  → OSV.dev API                         │
│  │  ├─ TyposquatDetector     │  → local curated popular-package list  │
│  │  ├─ LicenseChangeDetector │  → local curated license-change list   │
│  │  ├─ VersionInfoService    │  → NuGet V3 API                        │
│  │  └─ ReportGenerator       │  → console / JSON / SARIF              │
│  └───────────┬────────────┘                                          │
│              │ (local cache, SQLite, TTL-based)                      │
└──────────────┼─────────────────────────────────────────────────────--┘
               │  outbound HTTPS only, no data upload in Free tier
               ▼
   ┌─────────────────────┐        ┌──────────────────────────┐
   │   api.osv.dev        │        │   api.nuget.org (v3)      │
   │  (vulnerability data) │        │  (version + metadata)     │
   └─────────────────────┘        └──────────────────────────┘

   ── Pro tier only, additional path ──
               │
               ▼
   ┌─────────────────────────────┐
   │  NuGetGuard Cloud (thin API)  │  license validation, policy sync,
   │  small managed DB + alerting  │  alert dispatch to Slack/Teams
   └─────────────────────────────┘
```

## 6. Key Design Decisions

1. **Local-first, cloud-optional.** The Free tier never talks to our own servers — only to OSV.dev and NuGet.org, both public and free. This keeps our hosting cost at effectively $0 while the free tier grows.
2. **We don't rebuild vulnerability data — we curate on top of it.** OSV.dev already aggregates GitHub Security Advisories and is growing at ~1,500 new advisories/month across ecosystems. Our value-add is the *typosquat list* and *license-change list*, both small, hand-curated datasets that are hard to find anywhere else.
3. **Everything is cacheable.** Vulnerability data changes slowly; we cache OSV/NuGet responses locally (SQLite) with a short TTL (a few hours) to avoid hammering public APIs and to keep scans fast offline-adjacent.
4. **CI/CD and IDE integration over a dashboard.** Developers live in their build output and pull requests, not in a separate web app. The MVP prioritizes a build-time report (console + SARIF for GitHub/Azure DevOps annotations) over any UI.
5. **Everyday usefulness, not just alerts.** Even a completely clean scan shows the latest available version and a one-line "what changed" summary sourced from NuGet release notes / GitHub releases, so developers get value on every run, not only when something is broken. This is what drives daily/weekly habitual use rather than one-off installs.

## 7. Functional Requirements (MVP)

| ID | Requirement |
|---|---|
| FR-1 | Given a `.csproj` / `packages.lock.json` / `Directory.Packages.props`, list all direct + transitive package references with resolved versions. |
| FR-2 | For each package+version, query OSV.dev and report any known vulnerabilities (ID, severity, summary, fixed version). |
| FR-3 | For each package name, compare against a curated list of top ~500 NuGet packages using edit-distance to flag likely typosquats. |
| FR-4 | For each package, check against a curated "license changed" list and warn if the resolved version is post-change. |
| FR-5 | For each package, fetch the latest available version from NuGet V3 API and show a diff (current → latest) plus a short changelog summary when available. |
| FR-6 | Output results in at least 3 formats: human-readable console table, JSON, SARIF (for CI annotations). |
| FR-7 | Exit with non-zero status code when a **High/Critical** vulnerability is found, configurable via a `.nugetguard.json` policy file. |
| FR-8 | Ship as a `dotnet tool` (global/local) **and** an MSBuild `Task` package so it can run via `dotnet nugetguard scan` or automatically on `dotnet build`. |

## 8. Non-Functional Requirements

- **Performance:** full scan of a 100-package project should complete in under 10 seconds on a warm cache, under 30 seconds cold, using the OSV batch endpoint (`/v1/querybatch`, up to 1000 queries per call).
- **Reliability:** if OSV.dev or NuGet.org is unreachable, fail *open* with a clear warning (don't block the build on our own infra being down) — configurable.
- **Security:** the Free tier CLI must never transmit the user's actual source code or proprietary data anywhere — only public package name + version strings, which is not sensitive.
- **Portability:** cross-platform (Windows/Linux/macOS), matching standard `dotnet tool` requirements.
- **Extensibility:** data sources (OSV, NuGet, curated lists) must be pluggable so a Pro-tier private feed can be added later without rearchitecting.

## 9. External Dependencies

| Dependency | Purpose | Cost |
|---|---|---|
| `api.osv.dev` | Vulnerability data (OSV schema, batch endpoint) | Free, public, generous rate limits, no auth |
| `api.nuget.org/v3` | Package version + metadata (RegistrationsBaseUrl, PackageBaseAddress) | Free, public |
| GitHub repo releases / NuGet release notes (optional, phase 2) | "What changed" summaries | Free |

## 10. Roadmap Alignment

- **v0.1 (this HLD/LLD):** Free CLI + MSBuild task, 3 detectors (vuln, typosquat, license), console/JSON/SARIF output.
- **v0.2:** GitHub Action wrapper, `.nugetguard.json` policy file, SARIF upload to GitHub code scanning.
- **v0.3 (Pro tier begins):** license-key gating, cloud backend for continuous monitoring + Slack/Teams alerts, per-org private policy.
- **v0.4+:** private curated feed subscription, SBOM export, dashboard.

See the companion **Low-Level Design** document for module-level detail sufficient to start implementation, and the **NuGet Publishing Guide** for how to ship v0.1 to nuget.org under an MIT license.
