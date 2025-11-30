# .NET 10 (Preview) — Big Bang atomic upgrade

## Overview

Execute the selected Big Bang Strategy: simultaneously upgrade all projects to `net10.0`, replace/remove unsupported APIs (notably `BinaryFormatter`), restore & build, and validate by running the test suite. Tasks follow the plan's single-atomic-upgrade approach and include a single consolidated commit for the upgrade changes (per Plan §11).

**Progress**: 1/3 tasks complete (33%) ![33%](https://progress-bar.xyz/33)

## Tasks

### [✓] TASK-001: Verify prerequisites and prepare environment *(Completed: 2025-11-30 14:18)*
**References**: Plan §7 (Phase 0 Preparation), Plan §1 Executive Summary

- [✓] (1) Verify .NET 10 Preview SDK is installed and available on the executor environment and CI (`dotnet --info` or equivalent) per Plan §7.
- [✓] (2) If `global.json` exists, update it to reference the .NET 10 Preview SDK per Plan §7 (edit `global.json` to the preview SDK version listed in environment) (**Verify**).
- [✓] (3) Update CI pipeline configuration files to allow Preview SDK usage where required (per Plan §7) (**Verify**).
- [✓] (4) Confirm environment changes are present (SDK version reported is 10.x-preview) (**Verify**).

### [▶] TASK-002: Atomic framework upgrade, code refactor for breaking changes, restore & build, commit
**References**: Plan §8 Implementation Steps, Plan §4 Project-by-Project Migration Plans (src\TripleStore.Core, src\TripleStore.Storage, test\TripleStore.Tests), Plan §6 Breaking Changes Catalog

- [▶] (1) Update `TargetFramework` to `net10.0` in all project files:
  - `src\TripleStore.Core/TripleStore.Core.csproj`
  - `src\TripleStore.Storage/TripleStore.Storage.csproj`
  - `test/TripleStore.Tests/TripleStore.Tests.csproj`
  (small explicit list per Plan §4)  
- [ ] (2) Replace/remove `BinaryFormatter` usage in `src/TripleStore.Storage/LightningProvider.cs` per Plan §4.2 and Plan §6:
  - Remove `using System.Runtime.Serialization.Formatters.Binary;`
  - Implement supported serialization alternative (e.g., `System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj)`), or constrain `ToSpan<T>` to primitive types as described in Plan §4.2. Reference and apply the mitigation options in Plan §6.
- [ ] (3) Update any related serialization/deserialization helper methods (e.g., `ObjectToByteArray`, `FromSpan<T>`, `ToSpan<T>`) per Plan §4.2 guidance to be compatible with .NET 10 (ensure deterministic, safe encoding).
- [ ] (4) Restore dependencies for the solution (`dotnet restore`) (**Verify**: restore completes without errors).
- [ ] (5) Build the entire solution (`dotnet build`) to identify compile-time issues (Plan §8). Capture and record build errors.
- [ ] (6) Fix all compilation errors identified in step (5) according to Plan §6 Breaking Changes Catalog and project-specific notes in Plan §4; apply code changes required for BCL/API changes.
- [ ] (7) Rebuild solution and verify it completes with 0 errors (**Verify**: "Solution builds with 0 errors").
- [ ] (8) Create the single atomic commit that contains framework updates and the required code refactors with message: `"chore: atomic upgrade to .NET 10 (preview) across all projects"` (**Verify**: commit created and contains the TargetFramework updates and code changes referenced in Plan §11).

### [ ] TASK-003: Run tests, fix failures, final validation
**References**: Plan §9 Testing and Validation Strategy, Plan §4 (test project details), Plan §6 Breaking Changes Catalog

- [ ] (1) Run all unit tests in `test/TripleStore.Tests` (`dotnet test` with same target framework) (**Verify**: tests executed).
- [ ] (2) If tests fail, apply targeted fixes for test regressions or API changes (reference Plan §4 and Plan §6). After changes, rebuild and re-run the tests once.
- [ ] (3) All tests pass with 0 failures (**Verify**).
- [ ] (4) If test-related code changes were introduced in step (2), create a follow-up commit: `"fix(tests): address failures after .NET 10 upgrade"` (**Verify**: commit created, tests pass after commit).
- [ ] (5) (Optional, automatable) Trigger CI pipeline for `upgrade-to-NET10` branch to validate CI builds and tests under CI environment (per Plan §9) (**Verify**: CI run succeeds).

--- 

Generation checklist (applied):
- Strategy batching rules (Big Bang) used: prerequisites separated; project updates + package/compilation fixes combined atomically; testing separated.  
- Large lists referenced where appropriate; small project list is explicit.  
- No non-automatable/manual UI verification tasks included.  
- Commit action placed to follow plan's single-atomic-commit preference (Task-002 action).