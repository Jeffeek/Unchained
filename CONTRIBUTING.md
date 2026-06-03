# Contributing to Unchained

Thank you for your interest in contributing to Unchained — a free, MIT-licensed PDF engine for .NET.

## Branch policy

| Branch | Purpose |
|---|---|
| `master` | Main development line — always green |
| `release/<MAJOR>.<MINOR>.x` | Stable release branches (e.g. `release/0.1.x`) |
| `feat/<name>` | Feature work, branched from `master` |
| `fix/<name>` | Bug fixes |

All patches for a given minor version live on its `release/<MAJOR>.<MINOR>.x` branch. Tags (`v0.1.0`, `v0.1.1`, …) are created by the release script and push-triggered CI publishes them.

## Getting started

```powershell
git clone https://github.com/Jeffeek/Unchained
cd Unchained
pwsh scripts/Build/Build.ps1          # clean → restore → build → test
```

Requires .NET 9 SDK or later. All three target frameworks (`net8.0`, `net9.0`, `net10.0`) are tested automatically.

## Adding new features

Unchained is an independent MIT-licensed PDF engine built against the ISO 32000 specification. When considering new features, reference the PDF specification as the authoritative source — never third-party library code or documentation.

## Pull request checklist

- [ ] `pwsh scripts/Build/Build.ps1` passes locally (all 3 TFMs, 0 errors)
- [ ] New public types and members have XML doc comments
- [ ] New behaviour has tests; existing tests are not broken
- [ ] If a new NuGet dependency is added, version is in `Directory.Packages.props` and removed from the individual `.csproj`
- [ ] Commit messages are imperative mood, ≤72 chars (`Add PDF/A validator`, not `Added pdf a`)

## Testing real PDFs

Place PDFs under `tests/Unchained.Pdf.Tests/TestFiles/` — they are `.gitignore`d. The veraPDF corpus subset is in `TestFiles/veraPDF/`. The `RealPdfFixtures.LoadOrSkip` helper skips gracefully when a file is absent.

## Release process

Maintainer only:

```powershell
pwsh scripts/Release/Release.ps1 0.1.0   # or bash scripts/Release/Release.sh 0.1.0
```

The script validates semver, creates/checks out `release/0.1.x`, tags `v0.1.0`, pushes — CI then publishes to NuGet.org.
