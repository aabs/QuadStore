<!--
SYNC IMPACT REPORT
==================
Version change: (none) → 1.0.0
Modified principles: N/A (initial adoption)
Added sections:
  - Core Principles (7 principles)
  - Quality Standards
  - Development Workflow
  - Governance
Removed sections: N/A (initial adoption)
Templates reviewed:
  - .specify/templates/plan-template.md          ✅ compatible; Constitution Check gate already present
  - .specify/templates/spec-template.md          ✅ compatible; User Scenarios & Testing section aligns
  - .specify/templates/tasks-template.md         ⚠ NOTE: tests are marked "OPTIONAL" in the template,
                                                    but this constitution mandates them — implementers
                                                    MUST treat the test tasks as required, not optional.
  - .specify/templates/checklist-template.md     ✅ compatible; generic structure unaffected
Follow-up TODOs: none — all placeholders resolved.
-->

# QuadStore Constitution

## Core Principles

### I. Precision & Correctness

Correctness is non-negotiable and takes absolute precedence over delivery speed.

- Every public API MUST have a precise, documented contract (pre-conditions, post-conditions, invariants).
- Ambiguous or under-specified behaviour MUST be resolved before implementation begins — never worked
  around with a "good enough" approximation.
- Integer overflow, encoding errors, boundary conditions, and off-by-one errors are treated as bugs, not
  edge cases.
- Code that is known to be wrong MUST NOT be committed even temporarily. A failing test is an acceptable
  marker for incomplete work; silently incorrect code is not.

**Rationale**: QuadStore is systems-level infrastructure. Consumers depend on exact RDF semantics. A single
correctness defect can corrupt downstream knowledge graphs silently.

### II. Test-First Development (NON-NEGOTIABLE)

No production code MUST be written before a failing test exists that demands it.

- The Red-Green-Refactor cycle is strictly enforced: write a failing test → write the minimum code to
  make it pass → refactor under a green suite.
- Tests MUST be written before implementation is started, not added retrospectively.
- Tests MUST NEVER be disabled, skipped (`[Skip]`), or masked (e.g., `#if DEBUG`) to achieve a passing
  build. A failing test is the correct signal that work remains; suppressing it removes that signal.
- Minimum code coverage target: 80% across all modules; 85%+ overall; 100% on critical paths
  (query evaluation, encoding/decoding, persistence writes).
- The test suite MUST remain 100% green before any commit is considered complete.

**Rationale**: A quad store is a precision instrument. Test-first ensures behaviour is fully specified
before code exists, making regressions detectable and the contract explicit.

### III. Property-Based Testing

Where behaviour holds for a family of inputs, a property-based test MUST be used instead of, or in
addition to, example-based tests.

- `FsCheck.Xunit` is the standard property-testing library for this project.
- Properties MUST be written for: encoding round-trips, query result idempotence, index consistency
  invariants, serialisation/deserialisation, and any function with a well-defined algebraic law.
- Generators SHOULD be written for domain types (`UriNode`, `LiteralNode`, quad patterns, etc.) and
  placed in a shared `Generators` module within the test project.
- Property tests count toward the coverage requirement in Principle II.

**Rationale**: The combinatorial space of RDF terms and query patterns is too large for exhaustive
example-based testing. Property-based tests explore that space automatically and surface subtle bugs
that no hand-written example would reach.

### IV. End-to-End Verification

A feature is NOT done until it has an end-to-end test that exercises the complete path from public API
to observable output.

- Unit tests are necessary but not sufficient. Every feature MUST also have at least one integration
  or end-to-end test that validates the complete workflow (e.g., load TriG → query → verify results).
- End-to-end tests MUST use realistic data volumes and query patterns representative of actual use.
- W3C compliance test suites (TriG, SPARQL) MUST be kept green; regressions are release-blocking.
- No feature may be declared complete with only unit tests passing.

**Rationale**: Systems can pass all unit tests and still fail as an integrated whole. End-to-end tests
are the authoritative definition of "done".

### V. Performance by Design

Performance is a first-class correctness requirement for a systems-level store.

- Critical hot paths (triple lookup, index scan, encoding) MUST have documented performance targets
  (e.g., sub-200 ns per lookup, 200 K+ triples/sec load) that are verified by benchmarks.
- BenchmarkDotNet benchmarks live in `benchmark/TripleStore.Benchmarks/` and MUST be updated whenever
  a hot-path is modified.
- No regression in published benchmark figures is acceptable without an explicit, documented trade-off
  decision recorded in `docs/performance/`.
- Premature optimisation is prohibited. Optimise only after a measured baseline shows a need.

**Rationale**: QuadStore's primary value proposition is sub-microsecond query performance. Regressions
discovered late are expensive; benchmarks make them detectable immediately.

### VI. Simplicity & Minimalism

The simplest solution that meets the requirement MUST be preferred.

- YAGNI (You Aren't Gonna Need It) is enforced. No speculative features or abstractions for hypothetical
  future requirements.
- New external package dependencies require explicit justification. The dependency footprint MUST be kept
  minimal.
- Abstractions (interfaces, base classes, generic helpers) MUST be introduced only when there are at
  least two concrete implementations or call sites that genuinely benefit from them.
- Code complexity MUST be justified by a documented reason in the relevant plan or tasks file.

**Rationale**: Unnecessary complexity raises maintenance cost, obscures correctness properties, and makes
performance analysis harder. Simplicity is not a shortcut — it is an engineering discipline.

### VII. Living Documentation

Documentation MUST be accurate, current, and co-located with the code it describes.

- All documentation MUST be written in Markdown and stored under the `docs/` folder at the repository
  root.
- Document filenames MUST use simple snake_case (e.g., `query_engine.md`, `trig_loader.md`).
- Documents MUST be organised into coherent sub-folders by domain (e.g., `docs/architecture/`,
  `docs/api/`, `docs/performance/`). Ad-hoc or randomly placed documents are not permitted.
- Documentation MUST be updated as part of the same feature branch/commit that introduces the feature.
  Documentation left "for later" is a policy violation.
- The `tasks.md` file for a feature MUST be kept current throughout implementation, with task statuses
  updated as work progresses. It MUST NOT be updated only at the end.

**Rationale**: Stale documentation is actively harmful. Keeping docs in `docs/` with consistent naming
makes knowledge discoverable and auditable; treating doc updates as part of the definition of done
prevents documentation debt from accumulating.

## Quality Standards

- **Build**: `dotnet build -c Release` MUST succeed with zero warnings treated as errors on the CI path.
- **Test gate**: `dotnet test -c Release --nologo` MUST pass 100% before any PR is merged.
- **Coverage**: Measured per Principle II. Reported via `dotnet test --collect:"XPlat Code Coverage"`.
- **Benchmarks**: Run via `dotnet run -c Release --project benchmark/TripleStore.Benchmarks/` and
  compared against the baseline figures recorded in `benchmark/PERFORMANCE_CONTEXT.md`.
- **W3C Compliance**: The TriG and SPARQL compliance test suites MUST remain fully green.
- **Branch hygiene**: Feature branches follow the naming convention `###-short-description` where `###`
  is the issue or task number.
- **Technology stack**: .NET 10 (C#, `LangVersion latest`). No downgrade without a governance amendment.

## Development Workflow

1. **Specify**: Create or update `specs/###-feature/spec.md` before any code is written.
2. **Plan**: Produce `specs/###-feature/plan.md` confirming technical approach and Constitution Check.
3. **Task**: Generate `specs/###-feature/tasks.md`; all tasks start as `[ ]` not-started.
4. **RED**: Write failing tests (unit + property + integration). Confirm they fail for the right reason.
5. **GREEN**: Write the minimum implementation to make the tests pass.
6. **REFACTOR**: Improve code quality; suite MUST remain green throughout.
7. **Document**: Update `docs/` with any new or changed behaviour. Update `tasks.md` to reflect
   completion status in real time.
8. **End-to-End Gate**: Verify at least one end-to-end test exercises the complete feature path.
9. **Merge**: All quality gates (Principle II coverage, W3C suite, benchmarks) MUST be green.

Tasks MUST be marked in-progress when work begins and completed immediately upon finishing — not batched
at the end of a session.

## Governance

- This constitution supersedes all other development guidance for the QuadStore project. In the event
  of a conflict, the constitution wins.
- **Amendments** require: (a) documenting the proposed change and rationale, (b) updating this file
  with a version bump per the semantic versioning policy below, (c) updating any affected templates or
  guidance docs, and (d) recording the amendment in the Sync Impact Report comment at the top of this
  file.
- **Versioning policy**:
  - MAJOR: Backward-incompatible governance change — e.g., removing a principle or redefining a
    non-negotiable rule.
  - MINOR: New principle or section added, or materially expanded guidance.
  - PATCH: Clarifications, wording improvements, typo fixes, non-semantic refinements.
- **Compliance review**: Every PR description MUST include a "Constitution Check" section confirming
  which principles are relevant and how they are satisfied.
- **No exceptions** to Principles II (Test-First) and IV (End-to-End Verification) may be granted
  without a MAJOR version amendment to this constitution.

**Version**: 1.0.0 | **Ratified**: 2026-03-15 | **Last Amended**: 2026-03-15
