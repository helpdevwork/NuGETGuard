# Publishing NuGetGuard to NuGet.org (MIT License)

This is a practical, step-by-step guide for taking the built library (see HLD/LLD) live on nuget.org as a free, MIT-licensed package.

---

## 1. Add the MIT License to the repo

Create a `LICENSE` file at the repo root with the standard MIT text:

```
MIT License

Copyright (c) 2026 <Your Name>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

This only covers the **free/core** package. If you later split out a Pro package with a commercial license, that project ships its own separate `LICENSE` (or license-expression) and is **not** MIT — keep the two clearly separated at the project level so there's no ambiguity about what's open and what isn't.

## 2. Set package metadata in the `.csproj`

Each shippable project (`NuGetGuard.Cli`, `NuGetGuard.MSBuildTask`, and optionally `NuGetGuard.Core` if you want it consumable standalone) needs metadata:

```xml
<PropertyGroup>
  <PackageId>NuGetGuard.Cli</PackageId>
  <Version>0.1.0</Version>
  <Authors>Your Name</Authors>
  <Description>Scans .NET project dependencies for known vulnerabilities, typosquats, license changes, and outdated versions — at build time, before they become a problem.</Description>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageProjectUrl>https://github.com/yourname/nugetguard</PackageProjectUrl>
  <RepositoryUrl>https://github.com/yourname/nugetguard</RepositoryUrl>
  <PackageTags>security;nuget;vulnerability;scanner;supply-chain;sbom</PackageTags>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>icon.png</PackageIcon>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>

<ItemGroup>
  <None Include="../../README.md" Pack="true" PackagePath="\" />
  <None Include="../../icon.png" Pack="true" PackagePath="\" />
</ItemGroup>
```

Notes:
- `PackageLicenseExpression` with the SPDX identifier `MIT` is the current recommended approach (rather than a `licenseUrl`, which is being phased out).
- `PackageReadmeFile` makes your README render directly on the nuget.org package page — this matters a lot for adoption, since that page is your entire "landing page" for a tool with no marketing budget.
- A simple `icon.png` (128×128) helps the package look trustworthy/professional in search results — do not skip it.
- `dotnet tool` packages (the CLI) additionally need `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>nugetguard</ToolCommandName>` so `dotnet tool install -g NuGetGuard.Cli` exposes the `nugetguard` command.

## 3. Write the README that ships with the package

Keep it short and scannable — this is what a developer reads in the 10 seconds before deciding whether to install:

```markdown
# NuGetGuard

Catch known vulnerabilities, typosquats, and license-change surprises in your
NuGet dependencies — automatically, on every build.

## Install
    dotnet tool install --global NuGetGuard.Cli

## Use
    nugetguard scan

## What it checks
- Known vulnerabilities (via the OSV.dev database)
- Typosquatted package names
- Packages that recently switched to a commercial license
- Whether you're behind on the latest version, and what changed

MIT licensed. Free forever for individual use.
```

## 4. Create a nuget.org account and API key

1. Go to nuget.org and sign in (Microsoft account).
2. Go to your account settings → **API Keys** → create a new key.
   - Scope it to **Push new packages and package versions** only (least privilege).
   - Set an expiration (90–365 days) and rotate it — don't create a key with no expiry.
   - Optionally restrict it to a specific package ID glob (`NuGetGuard.*`) once the package exists, so a leaked key can't be used to push unrelated packages.
3. Store the key as a secret — **never commit it to the repo.** For local publishing, store it via `dotnet nuget setapikey`; for CI, store it as a GitHub Actions secret (`NUGET_API_KEY`).

## 5. Build and pack locally (first release)

```bash
dotnet restore
dotnet build -c Release
dotnet pack -c Release -o ./artifacts
```

This produces `NuGetGuard.Cli.0.1.0.nupkg` (and a `.snupkg` symbols package) in `./artifacts`.

## 6. Push to nuget.org

```bash
dotnet nuget push ./artifacts/NuGetGuard.Cli.0.1.0.nupkg \
  --api-key <YOUR_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

The symbols package (`.snupkg`) is picked up automatically if present alongside the `.nupkg`. First-time publishing of a new package ID may take a few minutes to a few hours to appear in search (it's indexed asynchronously), even though it's installable by exact ID almost immediately.

## 7. Automate future releases (GitHub Actions)

```yaml
# .github/workflows/publish.yml
name: Publish to NuGet
on:
  push:
    tags:
      - "v*"

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"
      - run: dotnet restore
      - run: dotnet build -c Release
      - run: dotnet pack -c Release -o ./artifacts
      - run: |
          dotnet nuget push ./artifacts/*.nupkg \
            --api-key ${{ secrets.NUGET_API_KEY }} \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate
```

Tag a release (`git tag v0.1.0 && git push --tags`) to trigger this. `--skip-duplicate` prevents the job from failing if a version was already pushed manually.

## 8. Versioning policy

Use SemVer strictly, since consumers of a security tool need to trust your versioning:
- `0.x.y` while in MVP/pre-1.0 — breaking changes allowed between minor versions, document them in a `CHANGELOG.md`.
- `1.0.0` once the CLI surface (§4 of the LLD) and JSON/SARIF output schema are stable enough that you're comfortable committing to backward compatibility.
- Any change to the JSON output schema after 1.0 is itself a breaking change for downstream consumers (e.g. a future Pro dashboard, or CI scripts parsing your JSON) — treat it accordingly.

## 9. Post-publish checklist

- [ ] Confirm the package page renders correctly on nuget.org (README, icon, license badge showing "MIT").
- [ ] Install it fresh in an empty test project to confirm the real end-user experience: `dotnet tool install --global NuGetGuard.Cli` then `nugetguard scan`.
- [ ] Add a nuget.org badge to your GitHub README (`![NuGet](https://img.shields.io/nuget/v/NuGetGuard.Cli)`).
- [ ] Publish the companion GitHub repo publicly with a clear `CONTRIBUTING.md` if you want community help maintaining the typosquat/license-change data files (LLD §3.3, §3.5) — that crowdsourced maintenance is part of what keeps the "moat" data current without it being a full-time job for you alone.

## 10. What stays separate for the future Pro tier

Keep the MIT-licensed free package and any future commercial/Pro component in **separate NuGet package IDs and separate repos (or clearly separated folders with distinct licenses)** from day one:
- `NuGetGuard.Cli` / `NuGetGuard.MSBuildTask` / `NuGetGuard.Core` → MIT, public, free forever.
- A future `NuGetGuard.Pro` (or a hosted backend, not distributed via NuGet at all) → separate license terms, license-key gated, never bundled into the MIT package.

This separation is what protects you legally and practically: nobody can point to the free package and claim entitlement to the paid features, because they were never in the same license boundary to begin with.
