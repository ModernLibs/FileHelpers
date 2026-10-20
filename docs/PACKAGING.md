# Packaging and releases

This page is for maintainers. It describes the NuGet package, the version scheme and the release
steps.

## The package

One package is published: **ModernLibs.FileHelpers**. The source generator is not a separate
package; it is embedded in the library package as an analyzer.

```
ModernLibs.FileHelpers.<version>.nupkg
├── lib/net10.0/ModernLibs.FileHelpers.dll
├── lib/net10.0/ModernLibs.FileHelpers.xml                 XML documentation
├── analyzers/dotnet/cs/ModernLibs.FileHelpers.Generator.dll  source generator and analyzer
├── build/ModernLibs.FileHelpers.props                      FileHelpersMaxFieldWidth (direct references)
├── buildTransitive/ModernLibs.FileHelpers.props            the same, for transitive references
├── README.md                                               package readme (the repository README)
├── icon.png
└── LICENSE
```

- The package targets `net10.0` only and has no package dependencies. It requires C# 11 or later
  (`MLFH026`): the generated code uses `file`-scoped types, so below C# 11 nothing is generated and
  every record type reports `MLFH026`. The .NET 10 default is C# 14, so this only affects projects that
  pin an older `LangVersion`.
- `ModernLibs.FileHelpers.Generator.dll` contains both the source generator and the analyzer
  (`FileHelpersAnalyzer`) that reports the `MLFH` diagnostics; the generator reports only `MLFH020`
  itself. `MLFH` warnings can be suppressed as usual; errors that block the descriptor cannot
  (`#pragma`, `.editorconfig`, `NoWarn` are ignored). If they are hidden with `[SuppressMessage]` or
  `RunAnalyzers=false`, the generator registers a descriptor for the record that throws a
  `BadUsageException` naming the errors (see
  [suppressing diagnostics](diagnostics.md#suppressing-diagnostics)).
- The `.props` file makes the `FileHelpersMaxFieldWidth` MSBuild property visible to the generator.
  It sets the width above which `[FieldFixedLength]` reports `MLFH007` (default 1000):

  ```xml
  <PropertyGroup>
    <FileHelpersMaxFieldWidth>2000</FileHelpersMaxFieldWidth>
  </PropertyGroup>
  ```

  The same setting can be given as `filehelpers.max_field_width = 2000` in a `.globalconfig` file
  (with `is_global = true`). When both are set, the MSBuild property wins; a value that is not a
  positive integer is ignored, and the next source (then the default) applies.
- `README.md` is packed from the repository root and shown on nuget.org. nuget.org does not resolve
  relative links, so every link in it must be an absolute `https://github.com/ModernLibs/FileHelpers/...`
  URL, and images must use absolute raw URLs.
- A symbol package (`.snupkg`) is produced next to the `.nupkg`. Source Link is enabled
  (`PublishRepositoryUrl`, `EmbedUntrackedSources`, and `ContinuousIntegrationBuild` on GitHub
  Actions), so debuggers can step into the library source.

Build a package locally:

```bash
dotnet pack src/ModernLibs.FileHelpers -c Release -p:Version=4.8.0 -o artifacts
```

`dotnet pack` builds the generator project first (it is a project reference) and copies its
`netstandard2.0` output into `analyzers/dotnet/cs/`.

## Versions

ModernLibs.FileHelpers 4.x is a ground-up reimplementation that follows FileHelpers 3.x, so it starts
at major version 4.

| Versions | What they are |
|---|---|
| 4.0.0 to 4.7.0 | Development milestones. Published only as source releases on GitHub, never on NuGet. |
| 4.8.0 | Release-readiness milestone and the **first NuGet release**. |
| after 4.8.0 | [Semantic Versioning 2.0](https://semver.org/): MAJOR for breaking changes, MINOR for backward-compatible features, PATCH for fixes. |

The public API is tracked with `Microsoft.CodeAnalysis.PublicApiAnalyzers` (`PublicAPI.Shipped.txt`
and `PublicAPI.Unshipped.txt` in the library project). Within a major version, nothing listed in
`PublicAPI.Shipped.txt` is removed or changed incompatibly. Types marked
`[EditorBrowsable(EditorBrowsableState.Never)]` (for example `RecordOptions`, `ConverterBaseAdapter<T>`,
`FileHelperRegistry<T>.SetDescriptor` and the `FieldDescriptor` constructor) exist for generated code.
They are public because generated code in the consuming assembly binds to them, but they are not
intended to be called directly.

Pre-release versions use SemVer suffixes, which sort `alpha < beta < rc < release`:

| Suffix | Meaning | Example |
|---|---|---|
| `-alpha.N` | Early, incomplete | `4.9.0-alpha.1` |
| `-beta.N` | Feature-complete, being tested | `4.9.0-beta.1` |
| `-rc.N` | Release candidate | `4.9.0-rc.1` |

## Publishing pipeline

`.github/workflows/publish.yml` publishes to nuget.org with
[NuGet Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): GitHub
Actions exchanges a short-lived OIDC token for a one-hour nuget.org API key. No long-lived API key is
stored anywhere.

### When it publishes

A run publishes only if **both** rules hold; otherwise it fails before packing:

1. The tag has the form `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-prerelease`, and the version
   (the tag without `v`) equals `<Version>` in `src/ModernLibs.FileHelpers/ModernLibs.FileHelpers.csproj`
   at the tagged commit.
2. The version is at least 4.8.0, the first NuGet release.

Rule 2 stops this workflow from publishing the milestone versions 4.0.0 to 4.7.0, for example through a
manual run with `tag: v4.5.0`. It does not cover a re-pushed milestone tag: a tag push runs the
`publish.yml` of the tagged commit, and the milestone commits from `v4.3.0` on carry an older
`publish.yml` without rule 2. That older workflow pushes with a `NUGET_API_KEY` secret, so a re-pushed
milestone tag cannot publish only because that secret does not exist (see the warning under
[One-time setup](#one-time-setup-repository-owner)).

### Triggers

- **Pushing a tag** `v*`: validate, build (with `-p:Version`, so the assemblies carry the release
  version), run the TUnit and xUnit suites (a run with no tests fails), pack the library, verify the
  package contents and version stamp, then publish and create a GitHub release. Versions with a `-`
  suffix are marked as pre-releases.
- **Manual run** (`workflow_dispatch`) with inputs `tag` (an existing tag, for example `v4.8.0`) and
  `dry_run` (default `true`). A dry run stops after packing and uploads the package as a workflow
  artifact; set `dry_run` to `false` to publish an existing tag. When the `nuget` environment only
  allows tags `v*`, start a publishing run from that tag (**Use workflow from** the tag, not `main`);
  dry runs do not use the environment and can start from any branch.

The publish job runs in the GitHub environment `nuget` with `id-token: write` (for the OIDC token) and
`contents: write` (for the release), and logs in with `NuGet/login` (v1). Every action in the
workflows is pinned to a full commit SHA, with the release version as a trailing comment; update the
SHA and the comment together.

### One-time setup (repository owner)

1. On nuget.org, add a **Trusted Publishing policy** for the package owner account:
   - Repository Owner: `ModernLibs`
   - Repository: `FileHelpers`
   - Workflow File: `publish.yml`
   - Environment: `nuget`
2. In the GitHub repository settings, create the **environment** `nuget`. Optionally add protection
   rules (required reviewers, deployment tags `v*`).
3. Add the GitHub **secret** `NUGET_USER` with the nuget.org profile name (not the e-mail address) of
   the account that owns the policy.

> **Never create a `NUGET_API_KEY` secret.** The commits of the milestone tags carry an older
> `publish.yml` that pushes with that secret, so creating it would let a re-pushed milestone tag
> publish. The current workflow keeps an API-key push only as a commented-out fallback.

## Release checklist

1. All tests pass (see [CONTRIBUTING.md](../CONTRIBUTING.md)): TUnit tests, mutation-target tests and
   the fuzz seed corpus.
2. `CHANGELOG.md`: move the `Unreleased` entries under the new version and set the date.
3. Public API: move the entries of `src/ModernLibs.FileHelpers/PublicAPI.Unshipped.txt` to
   `PublicAPI.Shipped.txt`.
4. Diagnostics: move new rules from
   `src/ModernLibs.FileHelpers.Generator/Diagnostics/AnalyzerReleases.Unshipped.md` to
   `AnalyzerReleases.Shipped.md` under the new version, and check that each is documented in
   [diagnostics.md](diagnostics.md).
5. Version: set `<Version>` in `src/ModernLibs.FileHelpers/ModernLibs.FileHelpers.csproj` to the new
   version and commit it. The tag must match it exactly.
6. Optionally check the package locally with `dotnet pack` (above), or run the workflow manually with
   `dry_run: true` and download the artifact.
7. Tag the version commit and push the tag: `git tag v<version>`, then `git push origin v<version>`.
8. Check that the package and its readme appear correctly on nuget.org, and that the GitHub release
   was created.

## Consuming a package

```bash
dotnet add package ModernLibs.FileHelpers --version 4.8.0
dotnet add package ModernLibs.FileHelpers --prerelease      # latest pre-release
```

```xml
<PackageReference Include="ModernLibs.FileHelpers" Version="4.8.0" />
```
