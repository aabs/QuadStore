# Implementation Plan: NuGet Publish GitHub Action

## Overview

Implement a GitHub Actions workflow that builds, tests, and publishes QuadStore.Core and QuadStore.SparqlServer as NuGet packages to nuget.org on version tag pushes. Add required NuGet package metadata to both `.csproj` files and write property-based tests to validate version extraction and metadata completeness.

## Tasks

- [x] 1. Add NuGet package metadata to project files
  - [x] 1.1 Add NuGet metadata to QuadStore.Core .csproj
    - Add a `<PropertyGroup>` with PackageId, Authors, Description, PackageLicenseExpression, PackageProjectUrl, RepositoryUrl, and PackageReadmeFile to `src/QuadStore.Core/QuadStore.Core.csproj`
    - Add an `<ItemGroup>` entry to include `../../README.md` with `Pack="true"` and `PackagePath="/"`
    - Use the exact values from the design document Component 2
    - _Requirements: 6.1, 6.3, 6.5, 6.6_

  - [x] 1.2 Add NuGet metadata to QuadStore.SparqlServer .csproj
    - Add a `<PropertyGroup>` with PackageId, Authors, Description, PackageLicenseExpression, PackageProjectUrl, RepositoryUrl, and PackageReadmeFile to `src/QuadStore.SparqlServer/QuadStore.SparqlServer.csproj`
    - Add an `<ItemGroup>` entry to include `../../README.md` with `Pack="true"` and `PackagePath="/"`
    - Use the exact values from the design document Component 3
    - _Requirements: 6.2, 6.4, 6.5, 6.6_

- [x] 2. Create the GitHub Actions publish workflow
  - [x] 2.1 Create `.github/workflows/publish.yml` with trigger and environment setup
    - Create the `.github/workflows/` directory and `publish.yml` file
    - Configure `on: push: tags: ['v*']` trigger
    - Set `runs-on: ubuntu-latest`
    - Add `actions/checkout@v4` step with `fetch-depth: 0`
    - Add `actions/setup-java@v4` step with `distribution: temurin` and `java-version: 21`
    - Add `actions/setup-dotnet@v4` step with `dotnet-version: 10.0.x`
    - _Requirements: 1.1, 1.2, 1.3, 7.1, 7.2, 8.1, 8.2, 8.3, 8.4_

  - [x] 2.2 Add build, test, pack, and publish steps to the workflow
    - Add `dotnet restore QuadStore.sln` step
    - Add `dotnet build QuadStore.sln -c Release --no-restore` step
    - Add `dotnet test QuadStore.sln -c Release --no-build --nologo` step
    - Add version extraction step: `echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_ENV`
    - Add `dotnet pack` steps for both QuadStore.Core and QuadStore.SparqlServer with `-c Release --no-build /p:Version=${{ env.VERSION }} -o ./nupkgs`
    - Add `dotnet nuget push ./nupkgs/*.nupkg` step with `--source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate`
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3_

- [x] 3. Checkpoint - Verify build succeeds with metadata changes
  - Ensure `dotnet build -c Release` succeeds locally with the updated .csproj files
  - Ensure all existing tests pass with `dotnet test -c Release --nologo`
  - Ask the user if questions arise

- [x] 4. Write property-based tests for workflow correctness
  - [x] 4.1 Write property test for version tag pattern matching
    - **Property 1: Version tag pattern matching**
    - Create a test class in `test/QuadStore.Tests/` for publish workflow properties
    - Use `[Property]` attribute from FsCheck.Xunit
    - Generate random strings; verify that strings starting with `v` match the `v*` glob pattern and strings not starting with `v` do not match
    - Add comment tag: `// Feature: nuget-publish-github-action, Property 1: Version tag pattern matching`
    - **Validates: Requirements 1.1, 1.2, 1.3**

  - [x] 4.2 Write property test for version extraction round-trip
    - **Property 2: Version extraction preserves semver**
    - Generate random semver strings (`{major}.{minor}.{patch}` and `{major}.{minor}.{patch}-{prerelease}`), prefix with `v`, strip the `v`, and assert the result equals the original version string
    - Add comment tag: `// Feature: nuget-publish-github-action, Property 2: Version extraction preserves semver`
    - **Validates: Requirements 4.1, 4.3**

  - [x] 4.3 Write property test for required NuGet metadata fields
    - **Property 3: Required NuGet metadata fields are present in all publishable projects**
    - For each publishable project path (`src/QuadStore.Core/QuadStore.Core.csproj`, `src/QuadStore.SparqlServer/QuadStore.SparqlServer.csproj`), parse the `.csproj` XML and assert that each required field (PackageId, Authors, Description, PackageLicenseExpression, PackageProjectUrl, RepositoryUrl, PackageReadmeFile) exists with a non-empty value
    - Add comment tag: `// Feature: nuget-publish-github-action, Property 3: Required NuGet metadata fields present`
    - **Validates: Requirements 6.1, 6.2**

- [x] 5. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass with `dotnet test -c Release --nologo`
  - Verify the workflow YAML is valid and contains all required steps
  - Ask the user if questions arise

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- The workflow file and .csproj metadata use YAML and XML respectively — no C# code generation needed for the core implementation
- Property tests use FsCheck.Xunit (already a dependency in the test project)
- The user must manually configure the `NUGET_API_KEY` secret in GitHub repository settings before the first tag push
