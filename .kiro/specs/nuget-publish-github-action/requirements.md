# Requirements Document

## Introduction

This feature adds a GitHub Actions workflow that builds, tests, and publishes the QuadStore.Core and QuadStore.SparqlServer projects as NuGet packages to nuget.org. The workflow is triggered by pushing a version tag and ensures that only tested, release-quality packages are published.

## Glossary

- **Publish_Workflow**: The GitHub Actions workflow responsible for building, testing, packing, and pushing NuGet packages
- **QuadStore.Core**: The main library project at `src/QuadStore.Core/` providing the RDF quad store, indexes, TriG loader, and SPARQL engine
- **QuadStore.SparqlServer**: The ASP.NET Core library project at `src/QuadStore.SparqlServer/` providing SPARQL HTTP endpoint middleware
- **NuGet_Package**: A `.nupkg` artifact produced by `dotnet pack` containing a compiled library and its metadata
- **Version_Tag**: A Git tag matching the pattern `v*` (e.g. `v1.0.0`) used to trigger the Publish_Workflow
- **NuGet_API_Key**: A secret stored in the GitHub repository settings used to authenticate with nuget.org when pushing packages
- **Package_Metadata**: The set of MSBuild properties in a `.csproj` file that define NuGet package identity (PackageId, Version, Authors, Description, License, etc.)

## Requirements

### Requirement 1: Workflow Trigger

**User Story:** As a maintainer, I want the publish workflow to run only when I push a version tag, so that packages are published intentionally and not on every commit.

#### Acceptance Criteria

1. WHEN a Git tag matching the pattern `v*` is pushed, THE Publish_Workflow SHALL execute
2. WHEN a commit is pushed without a version tag, THE Publish_Workflow SHALL NOT execute
3. WHEN a pull request is opened or updated, THE Publish_Workflow SHALL NOT execute

### Requirement 2: Build Verification

**User Story:** As a maintainer, I want the workflow to build the solution in Release configuration before publishing, so that only compilable code produces packages.

#### Acceptance Criteria

1. WHEN the Publish_Workflow executes, THE Publish_Workflow SHALL restore NuGet dependencies for the solution
2. WHEN the Publish_Workflow executes, THE Publish_Workflow SHALL build the solution using `dotnet build -c Release`
3. IF the build fails, THEN THE Publish_Workflow SHALL stop execution and report the failure without publishing any packages

### Requirement 3: Test Verification

**User Story:** As a maintainer, I want all tests to pass before packages are published, so that only validated code reaches consumers.

#### Acceptance Criteria

1. WHEN the build succeeds, THE Publish_Workflow SHALL run `dotnet test -c Release --no-build --nologo`
2. IF any test fails, THEN THE Publish_Workflow SHALL stop execution and report the failure without publishing any packages

### Requirement 4: Package Creation

**User Story:** As a maintainer, I want NuGet packages created from the version tag, so that package versions match the Git tag.

#### Acceptance Criteria

1. WHEN tests pass, THE Publish_Workflow SHALL extract the version number from the Version_Tag by stripping the leading `v` prefix
2. WHEN packing, THE Publish_Workflow SHALL run `dotnet pack` in Release configuration with `--no-build` for both QuadStore.Core and QuadStore.SparqlServer
3. WHEN packing, THE Publish_Workflow SHALL set the package version to the extracted version number using the `/p:Version` MSBuild property
4. THE Publish_Workflow SHALL produce one NuGet_Package for QuadStore.Core and one NuGet_Package for QuadStore.SparqlServer

### Requirement 5: Package Publishing

**User Story:** As a maintainer, I want packages pushed to nuget.org automatically, so that consumers can install them via `dotnet add package`.

#### Acceptance Criteria

1. WHEN both NuGet_Packages are created successfully, THE Publish_Workflow SHALL push each NuGet_Package to nuget.org using `dotnet nuget push`
2. THE Publish_Workflow SHALL authenticate with nuget.org using the NuGet_API_Key stored as a GitHub repository secret
3. IF a package with the same version already exists on nuget.org, THEN THE Publish_Workflow SHALL skip that package and continue without failing

### Requirement 6: Package Metadata

**User Story:** As a consumer, I want the NuGet packages to have proper metadata, so that I can identify and evaluate them on nuget.org.

#### Acceptance Criteria

1. THE QuadStore.Core project SHALL define Package_Metadata including PackageId, Authors, Description, PackageLicenseExpression, PackageProjectUrl, and RepositoryUrl
2. THE QuadStore.SparqlServer project SHALL define Package_Metadata including PackageId, Authors, Description, PackageLicenseExpression, PackageProjectUrl, and RepositoryUrl
3. THE QuadStore.Core Package_Metadata SHALL set PackageId to `QuadStore.Core`
4. THE QuadStore.SparqlServer Package_Metadata SHALL set PackageId to `QuadStore.SparqlServer`
5. THE Package_Metadata for both projects SHALL set PackageLicenseExpression to `MIT`
6. THE Package_Metadata for both projects SHALL include a PackageReadmeFile referencing the repository README

### Requirement 7: ANTLR Parser Generation in CI

**User Story:** As a maintainer, I want the CI environment to support ANTLR parser generation, so that QuadStore.Core builds successfully in GitHub Actions.

#### Acceptance Criteria

1. WHEN the Publish_Workflow executes, THE Publish_Workflow SHALL ensure a Java runtime is available on the runner for ANTLR parser generation
2. WHEN the Publish_Workflow executes, THE Publish_Workflow SHALL use the .NET 10.0 SDK for building and packing

### Requirement 8: Workflow Environment

**User Story:** As a maintainer, I want the workflow to run on a standard GitHub-hosted runner, so that no custom infrastructure is needed.

#### Acceptance Criteria

1. THE Publish_Workflow SHALL run on an `ubuntu-latest` GitHub-hosted runner
2. THE Publish_Workflow SHALL use the `actions/checkout` action to retrieve the repository source code including tags
3. THE Publish_Workflow SHALL use the `actions/setup-dotnet` action to install the .NET 10.0 SDK
4. THE Publish_Workflow SHALL use the `actions/setup-java` action to install a Java runtime for ANTLR parser generation
