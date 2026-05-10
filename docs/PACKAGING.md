# Packaging Guide for ModernLibs.FileHelpers

## Versioning Scheme

ModernLibs.FileHelpers follows [SemVer 2.0](https://semver.org/):

- **MAJOR.MINOR.PATCH** — e.g., `4.0.0`
- **MAJOR** — breaking API changes
- **MINOR** — new features, backward-compatible
- **PATCH** — bug fixes, backward-compatible

## Pre-release Suffixes

| Suffix | Meaning | Example |
|--------|---------|---------|
| `-alpha.N` | Early experimental builds | `4.0.0-alpha.1` |
| `-beta.N` | Feature-complete, testing phase | `4.0.0-beta.2` |
| `-rc.N` | Release candidate, near-final | `4.0.0-rc.1` |
| *(none)* | Stable release | `4.0.0` |

Pre-release versions sort lexicographically by suffix: `alpha < beta < rc < stable`.

## Building a Package Locally

### Stable release
```bash
dotnet pack src/ModernLibs.FileHelpers/ -c Release -p:Version=4.0.0 --output nupkgs
```

### Pre-release
```bash
dotnet pack src/ModernLibs.FileHelpers/ -c Release -p:Version=4.0.0-rc.1 --output nupkgs
```

### What gets produced
- `ModernLibs.FileHelpers.<version>.nupkg` — main package
- `ModernLibs.FileHelpers.<version>.snupkg` — symbol package (source link enabled)

### Package contents
```
ModernLibs.FileHelpers.<version>.nupkg
├── lib/net10.0/ModernLibs.FileHelpers.dll
├── lib/net10.0/ModernLibs.FileHelpers.xml    # XML documentation
├── analyzers/dotnet/cs/ModernLibs.FileHelpers.Generator.dll  # embedded source generator
├── README.md                                  # package readme
├── icon.png                                   # package icon
├── LICENSE                                    # MIT license
└── ModernLibs.FileHelpers.nuspec              # package metadata
```

## Consuming a Package

### Stable
```bash
dotnet add package ModernLibs.FileHelpers --version 4.0.0
```

### Pre-release
```bash
dotnet add package ModernLibs.FileHelpers --version 4.0.0-rc.1 --prerelease
```

### Via .csproj
```xml
<PackageReference Include="ModernLibs.FileHelpers" Version="4.0.0" />
```

## CI/CD Publish Workflow

Publishing is triggered by pushing a git tag matching `v*`:

```bash
git tag v4.0.0 && git push origin v4.0.0       # stable
git tag v4.0.0-rc.1 && git push origin v4.0.0-rc.1  # pre-release
```

The `.github/workflows/publish.yml` workflow:
1. Detects version from tag
2. Builds in Release configuration
3. Runs full test suite (TUnit + xUnit mutation)
4. Packs library and generator
5. Publishes to NuGet.org (requires `NUGET_API_KEY` secret)
6. Creates a GitHub Release with attached `.nupkg` artifacts
7. Marks as pre-release if tag contains `-`

## Release Checklist

Before tagging a release:

- [ ] All TUnit tests pass (`dotnet test --project tests/ModernLibs.FileHelpers.Tests/`)
- [ ] All xUnit mutation tests pass (`dotnet test --project tests/ModernLibs.FileHelpers.Mutation.Tests/`)
- [ ] Mutation score above threshold (see `stryker-config.json`)
- [ ] Benchmark regression suite passes (Phase 33+)
- [ ] CHANGELOG.md updated with release notes
- [ ] Version bumped in `src/ModernLibs.FileHelpers/ModernLibs.FileHelpers.csproj`
- [ ] Tag created and pushed: `git tag v<version> && git push origin v<version>`
- [ ] GitHub Release auto-created by CI workflow
- [ ] Package visible on nuget.org

## Source Link

Source Link is enabled for all Release builds. When debugging a consuming project, developers can step into ModernLibs.FileHelpers source directly from NuGet packages without downloading symbols separately.

Configuration:
- `PublishRepositoryUrl=true` — embeds repository URL in PDB
- `EmbedUntrackedSources=true` — embeds source files not tracked by git
- `ContinuousIntegrationBuild=true` — set automatically in GitHub Actions

## Symbol Packages

Symbol packages (`.snupkg`) are published to NuGet.org alongside the main package. They contain:
- PDB files with Source Link information
- Embedded source files (for untracked sources)

Consumers can debug into the library by enabling "Just My Code" off and "Source Link support" in their IDE.
