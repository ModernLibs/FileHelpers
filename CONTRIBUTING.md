# Contributing to ModernLibs.FileHelpers

Thank you for your interest. Bug reports, documentation fixes and pull requests are welcome.

- **Bugs**: open an issue with a minimal record type, a few input lines, what you expected and what
  happened. If the behaviour differs from FileHelpers 3.x, say so: 3.x is the reference for
  behaviour, and the [migration guide](docs/MIGRATION.md) lists the known differences.
- **Features**: open an issue first so we can agree on the API before you write code. Anything that
  needs run-time reflection or code generation cannot be accepted (see below).

## Repository layout

| Path | Contents |
|---|---|
| `src/ModernLibs.FileHelpers` | The library (`net10.0`) |
| `src/ModernLibs.FileHelpers.Generator` | The source generator and the analyzer that reports the `MLFH` diagnostics (`netstandard2.0`, packed into the library package) |
| `tests/ModernLibs.FileHelpers.Tests` | Main test suite (TUnit) |
| `tests/ModernLibs.FileHelpers.Mutation.Tests` | xUnit tests used as the Stryker.NET mutation-testing target |
| `tests/ModernLibs.FileHelpers.Fuzz.Tests` | SharpFuzz targets and seed corpus |
| `benchmarks/ModernLibs.FileHelpers.Benchmarks` | BenchmarkDotNet suite, see [BENCHMARKS.md](BENCHMARKS.md) |
| `samples/` | Sample applications |
| `docs/` | Documentation |

## Building and testing

You need the .NET 10 SDK version pinned in `global.json` (or a later patch of it).

```bash
dotnet build -c Release
```

The test projects run on [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
(`global.json` selects it for `dotnet test`):

```bash
# main suite (TUnit)
dotnet test --project tests/ModernLibs.FileHelpers.Tests -c Release

# mutation-target suite (xUnit)
dotnet test --project tests/ModernLibs.FileHelpers.Mutation.Tests -c Release
```

The test projects are also executables, which is how CI runs them:

```bash
./tests/ModernLibs.FileHelpers.Tests/bin/Release/net10.0/ModernLibs.FileHelpers.Tests
./tests/ModernLibs.FileHelpers.Tests/bin/Release/net10.0/ModernLibs.FileHelpers.Tests --treenode-filter "/*/*/ReadCoreAllocationTests/*"
```

CI also builds and runs the migration sample, which must keep working:

```bash
dotnet run --project samples/MigrationSamples -c Release
```

### Mutation testing

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) mutates the library and runs
the xUnit suite against each mutant. The configuration and thresholds are in `stryker-config.json`.

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker --config-file stryker-config.json
```

The HTML report is written to `StrykerOutput/`.

> **Currently informational only.** With Stryker.NET 5 and the xUnit v3 test project, coverage
> capture does not work and every mutant is reported as survived, so the mutation score is not
> meaningful. Stryker runs in its own workflow (`mutation.yml`, started manually and once a week) with
> `continue-on-error` until this is fixed; CI still requires the mutation-target xUnit suite to pass.

### Fuzzing

The fuzz project has four [SharpFuzz](https://github.com/Metalnem/sharpfuzz) targets (`delimited`,
`spanline`, `fixedlength`, `converters`) that read one input from standard input, plus an `extended`
mode that generates random inputs in-process. CI runs every target against the seed corpus:

```bash
dotnet build tests/ModernLibs.FileHelpers.Fuzz.Tests -c Release
for file in tests/ModernLibs.FileHelpers.Fuzz.Tests/FuzzCorpus/*.txt; do
  dotnet run --project tests/ModernLibs.FileHelpers.Fuzz.Tests -c Release --no-build -- delimited < "$file"
done

dotnet run --project tests/ModernLibs.FileHelpers.Fuzz.Tests -c Release -- extended 10000

# property-style random-input tests in the TUnit suite
./tests/ModernLibs.FileHelpers.Tests/bin/Release/net10.0/ModernLibs.FileHelpers.Tests --treenode-filter "/*/*/*/*[Category=Fuzz]"
```

For coverage-guided fuzzing, instrument the library with the SharpFuzz tool and run the targets
under AFL or libFuzzer as described in the SharpFuzz documentation. Add inputs that found bugs to
`FuzzCorpus/`.

### Benchmarks

```bash
dotnet run -c Release --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*'
```

Include before and after numbers, with your hardware and OS, in pull requests that claim a
performance change. See [BENCHMARKS.md](BENCHMARKS.md).

## Design constraints

These are requirements, not preferences. Pull requests that break them cannot be merged.

- **.NET 10 only.** The library targets `net10.0`; there is no multi-targeting.
- **AOT and trimming safe.** No reflection, `System.Reflection.Emit`, `Expression` compilation,
  `DynamicMethod`, `TypeDescriptor` or `MakeGenericType` in the library. Everything that depends on the
  record type is generated at compile time. The library is built with `IsAotCompatible` and
  `IsTrimmable`, so the analyzers report violations as build errors.
- **Allocation budget.** Reading a `struct` record whose fields are all value types must not allocate
  per record or per line on the callback and streaming paths; a `class` record allocates the record
  itself (and its strings). Tests such as `ReadCoreAllocationTests` enforce this. Do not use
  `string.Split`, `Regex` or `StringBuilder` in parsing hot paths; slice `ReadOnlySpan<char>` and use
  pooled buffers.
- **FileHelpers 3.x compatibility.** Attribute names, engine signatures, exception types and messages,
  and error-handling semantics follow 3.x. Where behaviour intentionally differs, document it in
  `docs/MIGRATION.md`.
- **Source generator.** It is an `IIncrementalGenerator`. Its pipeline must be deterministic and
  cacheable (equatable models, no `ISymbol` or `Compilation` in the models), and it emits `file`-scoped
  helper types. Generated code must compile with `ImplicitUsings` and nullable settings of any
  consuming project, so it uses fully qualified `global::` names, and must compile with C# 11 and
  without warnings at every warning level (`GeneratedCodeWarningTests` compiles a corpus of record
  shapes and fails on any warning in generated files). Report user mistakes as diagnostics with a real
  source location rather than generating code that fails at run time. Validation rules live in
  `Diagnostics/RecordValidator.cs`: `FileHelpersAnalyzer` reports them, and the generator runs the
  same rules to decide what to emit (it reports `MLFH020` alone). For a record with blocking errors
  the generator registers a descriptor whose members throw a `BadUsageException` naming the errors,
  so a record whose errors were hidden (`[SuppressMessage]`, `RunAnalyzers=false`) fails loudly.

## Coding standards

- Nullable reference types are enabled, and warnings are errors (`TreatWarningsAsErrors`) for the
  library, tests and benchmarks (not for the generator, see `Directory.Build.props`). The
  analyzer set is the .NET SDK analyzers (`AnalysisLevel=latest-recommended`),
  Roslynator and Meziantou.Analyzer. Fix warnings rather than suppressing them; if a suppression is
  needed, scope it narrowly and say why.
- Public classes are `sealed` unless they are designed for inheritance (for example the exception
  hierarchy and `ConverterBase`).
- Every public member has XML documentation (`CS1591` is enforced).
- Culture: parsing and formatting never depend on the current culture. Use the invariant culture or the
  culture the user configured.
- Library code that awaits uses `ConfigureAwait(false)`.

### Public API changes

The public API is tracked by `Microsoft.CodeAnalysis.PublicApiAnalyzers`:

- Adding a public member raises `RS0016`. Add it to `src/ModernLibs.FileHelpers/PublicAPI.Unshipped.txt`
  (the IDE code fix "Add to public API" does this).
- Removing or changing a member listed in `PublicAPI.Shipped.txt` is a breaking change and needs a new
  major version. Discuss it in an issue first.
- Types that exist only for generated code are marked `[EditorBrowsable(EditorBrowsableState.Never)]`
  and documented as such.

### Diagnostics

New diagnostics get the next free `MLFH` number, a rule in `RecordValidator`, an entry in the
analyzer release tracking files, a section in [docs/diagnostics.md](docs/diagnostics.md), and a help
link to that section. Messages say what is wrong and how to fix it.

- Release tracking: add new rules to
  `src/ModernLibs.FileHelpers.Generator/Diagnostics/AnalyzerReleases.Unshipped.md`; they move to `Shipped.md`
  at the next release (see the release checklist in [docs/PACKAGING.md](docs/PACKAGING.md)).
- Every error except `MLFH001` (reported at the usage site) and `MLFH020` (generator failure) is tagged
  `NotConfigurable`, so `#pragma`, `.editorconfig` and `NoWarn` cannot hide it, and blocks the
  descriptor (an attribute that cannot be applied, such as `MLFH004`, `MLFH006` or `MLFH010`, must not
  be ignored silently). Tag a new error the same way
  (`customTags: NotConfigurable` in `DiagnosticDescriptors`); the throwing descriptor
  (`Emission/InvalidRecordEmitter.cs`) lists it automatically. See
  [Suppressing diagnostics](docs/diagnostics.md#suppressing-diagnostics).

## Tests

- Work test-first: write a failing test that shows the bug or the missing behaviour, make it pass,
  then refactor.
- Use TUnit in `tests/ModernLibs.FileHelpers.Tests`. Give each new `[Test]` a readable
  `[DisplayName("...")]` (most existing tests have one; one older test class still uses the method
  names), and structure the body as Arrange / Act / Assert with a short comment for each:

  ```csharp
  [Test]
  [DisplayName("Empty int field without FieldNullValue raises NullValueNotFoundException")]
  public async Task EmptyInt_Throws()
  {
      // Arrange: a record with a non-nullable int field
      var engine = new FileHelperEngine<IntRecord>();

      // Act: read a line whose int field is empty
      var act = () => engine.ReadString(",x\r\n");

      // Assert: the 3.x exception type
      await Assert.That(act).Throws<NullValueNotFoundException>();
  }
  ```

- Test behaviour through the engines, not only that attributes construct or that code compiles.
- When a behaviour should match FileHelpers 3.x, check it against 3.x and say so in the test.
- Generator changes need generator tests (diagnostics and generated output) as well as engine tests.
  Add new record shapes or emission paths to the corpus in `GeneratedCodeWarningTests`, which requires
  warning-free generated code.
- Parsing changes should keep the allocation tests passing. Once mutation testing works again (see
  above), they should also keep the mutation score above the thresholds in `stryker-config.json`.

## Commits and pull requests

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<optional scope>): <summary in the imperative, lower case>

<optional body: what and why>
```

Types used in this repository: `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `build`, `ci` and
`chore`. Common scopes are `engines`, `generator`, `converters`, `parsing` and `api`. Mark breaking
changes with `!` after the type (`feat(api)!: ...`) and a `BREAKING CHANGE:` footer.

Before you open a pull request:

1. `dotnet build -c Release` has no warnings and both test suites pass.
2. Public API changes are recorded in `PublicAPI.Unshipped.txt`.
3. User-visible changes have an entry under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md), and the
   documentation (README, migration guide, diagnostics) is updated.
4. Keep pull requests focused: one logical change, in small commits.

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
