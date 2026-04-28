# ModernLibs.FileHelpers.Mutation.Tests

Mutation-test runner shell for the ModernLibs.FileHelpers specialist surface. The project hosts xUnit-runner-compatible cases that Stryker.NET mutates, so it is intentionally separate from the primary TUnit suite at `tests/ModernLibs.FileHelpers.Tests`.

## Status (v1.2 specialist closure — Phase 28)

This project currently discovers **zero** mutation-target tests. The placeholder file (`Placeholder.cs`) carries no `[Fact]`/`[Theory]` cases, so test discovery returns an empty set. Per Phase 28 decisions D-03/D-04, this is recorded as a **documented scoped exception** for `SPEC-02` rather than feature work.

The exception is auditable: the project still builds, runs, and exits cleanly via the `--ignore-exit-code 8` argument configured in `ModernLibs.FileHelpers.Mutation.Tests.csproj`.

## Reproducible commands

### Run the mutation-runner shell directly

```bash
dotnet run --project tests/ModernLibs.FileHelpers.Mutation.Tests/ModernLibs.FileHelpers.Mutation.Tests.csproj --configuration Release
```

Expected output: xUnit v3 reports `Total: 0` and the process exits with code 0 (because of `--ignore-exit-code 8`).

### Run via the testing platform

```bash
./tests/ModernLibs.FileHelpers.Mutation.Tests/bin/Release/net10.0/ModernLibs.FileHelpers.Mutation.Tests --ignore-exit-code 8
```

### Run Stryker.NET against the specialist surface

Stryker.NET is invoked separately as a global tool and is not yet wired into this project. When mutation tests are added in a future milestone, the recommended invocation is:

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker --project tests/ModernLibs.FileHelpers.Mutation.Tests/ModernLibs.FileHelpers.Mutation.Tests.csproj
```

## Why a scoped exception (D-03/D-04)

- The primary TUnit suite (`tests/ModernLibs.FileHelpers.Tests`) exercises the specialist surface end-to-end with 448/448 green tests; mutation regression risk is low.
- Stryker.NET requires xUnit/NUnit-style discovery, which means duplicating TUnit cases as xUnit `[Fact]` tests. That counts as feature work and is explicitly out of Phase 28 scope (D-02, D-09).
- Phase 28 closes the audit by documenting the gap with a reproducible command and a follow-up routing entry, not by expanding the suite.

## Follow-up routing

A mutation-target population task is captured in:

- `.planning/phases/24-specialist-verification-documentation/24-VERIFICATION.md` — closure verdict and scoped exception rationale.
- `.planning/phases/28-specialist-verification-traceability-closure/28-VERIFICATION-CHECKLIST.md` — Phase 28 success-criterion mapping (added by plan 28-03).
- `.planning/REQUIREMENTS.md` — `SPEC-02` traceability row pointing to this README.

When the next milestone re-opens specialist mutation work, replace `Placeholder.cs` with `[Fact]`/`[Theory]` cases for the targeted parsers (e.g. `MultiRecordEngine`, `MasterDetailEngine`, `FixedLengthSlicer`) and update this README's status section.
