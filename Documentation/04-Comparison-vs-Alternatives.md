# NuGetGuard vs. Alternatives

A honest comparison to the other tools .NET teams typically reach for when securing their dependency tree — so you can pick the right one (or combine them).

## Quick Summary

| | NuGetGuard | `dotnet list package --vulnerable` | Dependabot | Snyk |
|---|---|---|---|---|
| Vulnerability scanning | ✅ (OSV.dev) | ✅ (GitHub Advisory DB) | ✅ (GitHub Advisory DB) | ✅ (Snyk DB) |
| Typosquat detection | ✅ | ❌ | ❌ | ❌ |
| License change detection | ✅ | ❌ | ❌ | ⚠️ License *policy*, not *change* detection |
| Outdated version check | ✅ | ⚠️ separate `--outdated` flag | ✅ (PR-based) | ✅ |
| Runs locally / offline-first | ✅ | ✅ | ❌ (GitHub-hosted only) | ❌ (cloud service) |
| MSBuild integration (scan on every build) | ✅ | ❌ | ❌ | ⚠️ via separate plugin |
| SARIF output for PR annotations | ✅ | ❌ | N/A (built into GitHub UI) | ✅ |
| Cost | Free, MIT | Free (built into SDK) | Free | Free tier + paid plans |
| Setup | `dotnet tool install` | Built-in | Repo settings toggle | Account + integration |

## vs. `dotnet list package --vulnerable`

The .NET SDK has had a built-in vulnerability check since .NET 5:

```bash
dotnet list package --vulnerable --include-transitive
```

**What it does well**: zero install, always available, pulls from the GitHub Advisory Database.

**What it doesn't do**: that's the whole feature set. It only checks vulnerabilities — no typosquat detection, no license-change tracking, no SARIF output for CI annotations, and no MSBuild task to catch issues automatically on every build without a separate CI step. It's also framed as a manual, on-demand command rather than something built into your build or CI pipeline.

**When to use which**: `dotnet list package --vulnerable` is a fine quick local check. NuGetGuard is the better fit when you want the same class of check running automatically in CI/CD and at build time, plus the extra typosquat and license-change coverage that the SDK command doesn't offer.

## vs. Dependabot

GitHub's Dependabot is excellent at what it's designed for: automatically opening pull requests to bump vulnerable or outdated dependencies.

**What it does well**: fully automated PRs, tight GitHub integration, zero maintenance once configured, backed by the GitHub Advisory Database.

**What it doesn't do**: Dependabot only runs on GitHub-hosted repos and only reacts *after* a vulnerability is published for a package you already depend on — it won't warn you about a typosquatted package name before you add it, and it doesn't track license changes on packages you already use. It also runs on GitHub's schedule (not on every local build), so you don't get feedback until the PR shows up.

**When to use which**: They're complementary, not competing. Dependabot handles the "keep dependencies patched" loop well; NuGetGuard adds the checks Dependabot doesn't do (typosquats, license changes) and can run at build time in your own environment — including private repos or non-GitHub hosting, where Dependabot isn't available at all.

## vs. Snyk

Snyk is a mature, broad software composition analysis (SCA) platform covering many ecosystems beyond .NET, with a polished dashboard, license policy enforcement, and container/IaC scanning.

**What it does well**: breadth across languages and artifact types, a hosted dashboard for tracking findings over time, enterprise-grade policy management.

**What it doesn't do (as cleanly)**: Snyk is a cloud service — your dependency data leaves your environment, and the free tier has scan limits. Typosquat detection isn't a first-class Snyk feature for NuGet specifically. Setup requires an account, an integration, and (for full functionality) a paid plan.

**When to use which**: If you're already standardized on Snyk across multiple languages and want one dashboard for everything, it's a solid enterprise choice. If you specifically want a lightweight, free, MIT-licensed, .NET-only tool that runs entirely in your own CI/build environment with no external account or data sharing, NuGetGuard is the simpler fit — especially for small teams and OSS projects that don't need a full SCA platform.

## Why Not Just Pick One?

For most .NET teams, the practical setup is layered, not exclusive:

- **NuGetGuard's MSBuild task** — catches typosquats and license changes at build time, in every developer's inner loop, for free
- **Dependabot** — keeps dependencies patched automatically via PRs
- **NuGetGuard (or Snyk) in CI** — a final gate with SARIF annotations before merge

None of these tools fully substitute for another; they cover different points in the lifecycle. NuGetGuard's niche is being the free, MIT-licensed, build-time-and-CI tool that specifically adds typosquat and license-change detection — checks the SDK's own tooling and Dependabot don't do at all.

## Try It

```bash
dotnet tool install --global NuGetGuard.Cli
nugetguard scan
```

See [Installation & CLI Usage](01-Installation-and-CLI-Usage.md) for the full guide.
