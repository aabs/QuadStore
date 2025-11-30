# .NET Upgrade Plan

## 1. Executive Summary
- Scenario: Upgrade all projects from .NET 6 to .NET 10 (Preview).
- Scope: 3 SDK-style projects.
  - src\TripleStore.Core (ClassLibrary) – net6.0
  - src\TripleStore.Storage (ClassLibrary) – net6.0
  - test\TripleStore.Tests (DotNetCoreApp) – net6.0; depends on Core
- Target State: All projects target net10.0. Packages remain as assessed compatible unless otherwise noted. Solution builds clean; tests pass.

- Selected Strategy: Big Bang Strategy – All projects upgraded simultaneously in a single atomic operation.
  - Rationale: Small solution (3 projects), simple dependency chain, SDK-style projects, assessment shows packages are compatible or have no required updates.

- Complexity Assessment: Low
  - Justification: Small codebase counts (Core ~890 LOC, Storage ~110 LOC, Tests ~386 LOC); minimal packages; straightforward dependencies.

- Critical Issues:
  - BinaryFormatter deprecation/removal in modern .NET: `System.Runtime.Serialization.Formatters.Binary.BinaryFormatter` usage in `src/TripleStore.Storage/LightningProvider.cs` is not supported from .NET 7+ and will fail in .NET 10. Plan includes refactoring guidance.
  - .NET 10 is Preview: requires Preview SDK on developer/CI machines; potential ecosystem gaps.

- Recommended Approach: Big Bang Strategy. Atomic upgrade provides fastest path with unified dependency resolution and verification.

## 2. Migration Strategy

### 2.1 Approach Selection
- Chosen Strategy: Big Bang Strategy
- Strategy Rationale: 3 projects, low complexity, homogeneous SDK-style setup, packages marked compatible, test project present to validate end-to-end.
- Strategy-Specific Considerations:
  - Perform a single, atomic upgrade of target frameworks and package adjustments across all projects.
  - Restore and build solution once; resolve compile errors introduced by framework changes; then run tests.
  - Prefer a single consolidated commit for the atomic upgrade (see Source Control Strategy).

### 2.2 Dependency-Based Ordering
- Although the execution is atomic, dependency awareness informs risk focus:
  - Leaf/foundation: `TripleStore.Storage` (no deps), `TripleStore.Core` (no deps)
  - Root: `TripleStore.Tests` (depends on Core)
- Critical path: Storage/Core libraries must compile for the test project to build and run.

### 2.3 Parallel vs Sequential Execution
- Big Bang implies simultaneous updates to all projects (no intermediate states).
- Build/fix cycle will naturally address lower-level libraries first since dependants’ errors often cascade from them.

## 3. Detailed Dependency Analysis

### 3.1 Dependency Graph Summary
- TripleStore.Tests → TripleStore.Core
- TripleStore.Storage (independent)

### 3.2 Project Groupings (for understanding; upgrade is atomic)
- Phase 0: Preparation and tooling updates
- Phase 1: Atomic upgrade of all projects (Core, Storage, Tests)
- Phase 2: Tests and validation

## 4. Project-by-Project Migration Plans

### Project: src\TripleStore.Core\TripleStore.Core.csproj

- Current State
  - Dependencies: 0
  - Dependants: 1 (TripleStore.Tests)
  - Package Count: 0 (no explicit NuGet packages listed)
  - LOC: ~890
- Target State
  - Target Framework: net10.0
  - Updated Packages: 0
- Migration Steps
  1. Prerequisites: Ensure .NET 10 Preview SDK installed; update `global.json` if present to preview SDK.
  2. Framework Update: Set `TargetFramework` to `net10.0`.
  3. Package Updates: None required per assessment.
  4. Expected Breaking Changes: General BCL changes; monitor obsolete APIs during build.
  5. Code Modifications: Address any compile errors flagged post-upgrade.
  6. Testing Strategy: Covered by `TripleStore.Tests`.
  7. Validation Checklist
     - [ ] Dependencies resolve correctly
     - [ ] Builds without errors
     - [ ] Builds without warnings
     - [ ] Unit tests referencing this project pass
     - [ ] No security warnings

---

### Project: src\TripleStore.Storage\TripleStore.Storage.csproj

- Current State
  - Dependencies: 0
  - Dependants: 0
  - Package Count: 1 (LightningDB 0.14.0 – Compatible)
  - LOC: ~110
- Target State
  - Target Framework: net10.0
  - Updated Packages: 0 (LightningDB remains; re-verify at build)
- Migration Steps
  1. Prerequisites: Ensure .NET 10 Preview SDK installed; verify LightningDB native requirements for the target runtime.
  2. Framework Update: Set `TargetFramework` to `net10.0`.
  3. Package Updates: None required per assessment.
  4. Expected Breaking Changes
     - BinaryFormatter removal: `System.Runtime.Serialization.Formatters.Binary.BinaryFormatter` is not supported in .NET 10.
       - Affected file: `src/TripleStore.Storage/LightningProvider.cs`
       - Affected method: `ObjectToByteArray(object obj)` and `ToSpan<T>` fallback path
       - Replacement options:
         - Prefer a safe serializer (e.g., `System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj)` for serializable data)
         - Or constrain `ToSpan<T>` to supported primitives and remove object serialization fallback
         - If binary compatibility is required, consider `MemoryPack`, `protobuf-net`, or custom value struct encodings
     - Validate `MemoryMarshal.Cast<byte, T>(span).ToArray()[0]` logic for `FromSpan<T>`; consider `MemoryMarshal.Read<T>(span)` to avoid allocation/alignment issues.
  5. Code Modifications
     - Remove `using System.Runtime.Serialization.Formatters.Binary;`
     - Replace `ObjectToByteArray` implementation with a supported approach; for example:
       - `return JsonSerializer.SerializeToUtf8Bytes(obj);` (requires `using System.Text.Json;`)
       - Ensure symmetric deserialization where applicable, or disallow non-primitive types if round-trip is not required.
     - Review `ToSpan<T>` and ensure all used types have deterministic encoding.
  6. Testing Strategy
     - Add/adjust unit tests around serialization paths or the database key/value encoding, if present.
     - Smoke test LightningDB CRUD operations.
  7. Validation Checklist
     - [ ] Dependencies resolve correctly
     - [ ] Builds without errors
     - [ ] Builds without warnings
     - [ ] Serialization paths do not use BinaryFormatter
     - [ ] No security warnings

---

### Project: test\TripleStore.Tests\TripleStore.Tests.csproj

- Current State
  - Dependencies: 1 (TripleStore.Core)
  - Dependants: 0
  - Package Count: 6
    - AutoFixture.NUnit3 4.17.0 – Compatible
    - coverlet.collector 3.1.0 – Compatible
    - FluentAssertions 6.2.0 – Compatible
    - Microsoft.NET.Test.Sdk 16.11.0 – Compatible
    - NUnit 3.13.2 – Compatible
    - NUnit3TestAdapter 4.0.0 – Compatible
  - LOC: ~386
- Target State
  - Target Framework: net10.0
  - Updated Packages: None required per assessment
- Migration Steps
  1. Prerequisites: Ensure .NET 10 Preview SDK installed.
  2. Framework Update: Set `TargetFramework` to `net10.0`.
  3. Package Updates: None required per assessment; note that newer `Microsoft.NET.Test.Sdk` 17.x often improves compatibility/perf, but not mandated.
  4. Expected Breaking Changes: Minimal; refactor tests only if production API changes.
  5. Code Modifications: Address assertions or API changes surfaced by compilation.
  6. Testing Strategy: Run all tests; keep `coverlet.collector` working under net10.0.
  7. Validation Checklist
     - [ ] Dependencies resolve correctly
     - [ ] Builds without errors
     - [ ] Builds without warnings
     - [ ] All unit tests pass
     - [ ] No security warnings

## 5. Package Update Reference

### Common Package Updates (affecting multiple projects)
- No updates required per assessment.

### Project-Specific Notes
- TripleStore.Storage: `LightningDB` 0.14.0 – assessment marked compatible with net10.0.
- TripleStore.Tests: test packages listed as compatible; consider optional modernization to recent versions after framework upgrade stabilizes.

## 6. Breaking Changes Catalog (Expected/Monitored)
- BinaryFormatter removal (critical):
  - Namespace: `System.Runtime.Serialization.Formatters.Binary`
  - Impact: Compile-time errors or runtime NotSupported on .NET 7+; not available in .NET 10.
  - Mitigation: Replace with `System.Text.Json` or other supported serializer; or avoid serializing arbitrary objects.
- Potential BCL/runtime updates:
  - API obsoletions behavior tightened; nullable annotations may reveal warnings as errors if configured.
  - Encoding/BitConverter endianness assumptions: ensure consistent endianness for persisted data.
- MSBuild/SDK updates:
  - Validate any `Directory.Build.props/targets` or `Directory.Packages.props` files if present; align conditions with `net10.0`.

## 7. Implementation Timeline

### Phase 0: Preparation
- Verify .NET 10 Preview SDK installed across dev/CI and enable “use previews” if required.
- Update `global.json` (if present) to the .NET 10 Preview SDK.
- Confirm branch `upgrade-to-NET10` is active and up to date with `main`.

### Phase 1: Atomic Upgrade (single coordinated operation)
- Update all project files to `net10.0` simultaneously.
- Remove/replace BinaryFormatter usage and any blocked APIs.
- Restore dependencies and build the solution; fix all compilation errors.
- Deliverable: Solution builds with 0 errors.

### Phase 2: Test Validation
- Execute all test projects; address failures.
- Deliverable: All tests pass.

## 8. Detailed Execution Steps (Atomic)
1. Update TargetFramework for all projects:
   - src\TripleStore.Core\TripleStore.Core.csproj → net10.0
   - src\TripleStore.Storage\TripleStore.Storage.csproj → net10.0
   - test\TripleStore.Tests\TripleStore.Tests.csproj → net10.0
2. Update/Refactor code blocked by framework changes:
   - Remove BinaryFormatter; implement safe serialization or restrict supported types.
3. Restore and build the entire solution.
4. Fix all compilation errors/warnings resulting from framework/package changes.
5. Rebuild and verify 0 errors.
6. Run tests and fix failures until all pass.

## 9. Testing and Validation Strategy
- Per-Project
  - Build with warnings visible; aim for zero warnings when feasible.
  - Unit tests for affected areas (serialization and DB interactions) in Storage.
- Phase-Level
  - After atomic upgrade build, run all tests.
  - Verify integration path: Tests → Core; Storage CRUD smoke tests.
- Full Solution
  - CI pipeline run on `upgrade-to-NET10`.
  - Security/dependency scan (optional) to confirm no new vulnerabilities.

## 10. Risk Management

### 10.1 High-Risk Changes
| Project | Risk | Mitigation |
|---------|------|------------|
| TripleStore.Storage | BinaryFormatter unsupported | Replace with `System.Text.Json` or other supported serializer; add tests to validate persistence format |
| All | Preview SDK availability across environments | Document SDK installation; update `global.json`; enable preview usage in CI |

### 10.2 Contingency Plans
- If BinaryFormatter refactor is larger than expected:
  - Constrain `ToSpan<T>` usage to primitives and remove fallback serialization for arbitrary objects.
  - Feature-flag any new encoding logic and add migration tooling if persisted data format changes.
- If a package proves incompatible at runtime despite assessment:
  - Pin to alternative compatible version or replace with maintained fork.

## 11. Source Control Strategy
- Branching
  - Main upgrade branch: `upgrade-to-NET10` (created from `main`)
- Commit Strategy
  - Prefer a single atomic commit including all TargetFramework updates, package adjustments (if any), and code refactors required by the framework upgrade.
  - Commit message template: "chore: atomic upgrade to .NET 10 (preview) across all projects"
- Review and Merge
  - Open PR to `main` with detailed description and link to this plan and assessment.
  - Require at least one reviewer; validate CI builds/tests before merge.

## 12. Success Criteria
- Strategy-Specific
  - Big Bang atomic operation executed with a single coordinated upgrade.
- Technical
  - [ ] All projects target net10.0
  - [ ] All builds succeed without errors
  - [ ] All tests pass
  - [ ] Zero security vulnerabilities reported
- Quality
  - [ ] No known regressions
  - [ ] Code quality and warnings at or below pre-upgrade levels
  - [ ] Documentation updated (notably serialization changes in Storage)
- Process
  - [ ] Source control strategy followed with appropriate commit and PR

---

Notes and Assumptions
- No `Directory.Build.*` or `Directory.Packages.props` files requiring changes were identified in assessment; executor should still scan for these and ensure any conditional `TargetFramework` logic embraces `net10.0`.
- Assessment flagged all listed packages as compatible; if test infra indicates otherwise, revisit specific packages post-upgrade.
