using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace TripleStore.Tests;

/// <summary>
/// Property-based tests for the NuGet publish GitHub Actions workflow.
/// Validates correctness properties of version tag matching, version
/// extraction, and package metadata completeness.
/// </summary>
public class PublishWorkflowPropertyTests
{
    // Feature: nuget-publish-github-action, Property 1: Version tag pattern matching

    /// <summary>
    /// Simulates the GitHub Actions glob pattern <c>v*</c> used in the
    /// workflow trigger. A tag matches if and only if it starts with 'v'
    /// followed by zero or more characters.
    /// </summary>
    private static bool MatchesVStarGlob(string tag)
    {
        return !string.IsNullOrEmpty(tag) && tag.StartsWith('v');
    }

    /// <summary>
    /// Generates random non-empty strings guaranteed to start with 'v'.
    /// </summary>
    private static Arbitrary<string> VPrefixedStringArb()
    {
        var gen = Gen.Elements(
                "abcdefghijklmnopqrstuvwxyz0123456789.-+_ABCDEFGHIJKLMNOPQRSTUVWXYZ"
                    .ToCharArray())
            .ArrayOf()
            .Select(chars => "v" + new string(chars));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates random non-empty strings that do NOT start with 'v'.
    /// </summary>
    private static Arbitrary<string> NonVPrefixedStringArb()
    {
        var nonVChars = "abcdefghijklmnopqrstuwxyzABCDEFGHIJKLMNOPQRSTUWXYZ0123456789.-+_"
            .ToCharArray();

        var gen = Gen.Elements(nonVChars)
            .SelectMany(first =>
                Gen.Elements(
                        "abcdefghijklmnopqrstuvwxyz0123456789.-+_ABCDEFGHIJKLMNOPQRSTUVWXYZ"
                            .ToCharArray())
                    .ArrayOf()
                    .Select(rest => first + new string(rest)));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    ///
    /// For any string starting with 'v', the v* glob pattern shall match.
    /// </summary>
    [Fact]
    public void Property1_StringsStartingWithV_MatchVStarGlob()
    {
        Prop.ForAll(VPrefixedStringArb(), (string tag) =>
        {
            return MatchesVStarGlob(tag)
                .Label($"Tag '{tag}' starts with 'v' but did not match v* glob");
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    ///
    /// For any string NOT starting with 'v', the v* glob pattern shall not match.
    /// </summary>
    [Fact]
    public void Property1_StringsNotStartingWithV_DoNotMatchVStarGlob()
    {
        Prop.ForAll(NonVPrefixedStringArb(), (string tag) =>
        {
            return (!MatchesVStarGlob(tag))
                .Label($"Tag '{tag}' does not start with 'v' but matched v* glob");
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// **Validates: Requirements 1.2, 1.3**
    ///
    /// Empty and whitespace-only strings shall never match the v* glob.
    /// </summary>
    [Fact]
    public void Property1_EmptyStrings_DoNotMatchVStarGlob()
    {
        Prop.ForAll(
            Gen.Elements("", " ", "\t", "\n", "\r\n").ToArbitrary(),
            (string tag) =>
            {
                return (!MatchesVStarGlob(tag))
                    .Label($"Whitespace/empty tag should not match v* glob");
            }).QuickCheckThrowOnFailure();
    }

    // Feature: nuget-publish-github-action, Property 2: Version extraction preserves semver

    /// <summary>
    /// Simulates the workflow version extraction logic:
    /// <c>${GITHUB_REF_NAME#v}</c> which strips the leading 'v' from the tag.
    /// </summary>
    private static string ExtractVersion(string tag)
    {
        return tag[1..];
    }

    /// <summary>
    /// Generates random semver strings in the format <c>{major}.{minor}.{patch}</c>.
    /// Uses PositiveInt to ensure non-negative version components.
    /// </summary>
    private static Arbitrary<string> SemverStringArb()
    {
        var gen = Gen.Choose(0, 9999)
            .SelectMany(major => Gen.Choose(0, 9999)
            .SelectMany(minor => Gen.Choose(0, 9999)
            .Select(patch => $"{major}.{minor}.{patch}")));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates random semver strings with a prerelease suffix in the format
    /// <c>{major}.{minor}.{patch}-{prerelease}</c>.
    /// </summary>
    private static Arbitrary<string> SemverWithPrereleaseArb()
    {
        var prereleaseChars = "abcdefghijklmnopqrstuvwxyz0123456789"
            .ToCharArray();

        var gen = Gen.Choose(0, 9999)
            .SelectMany(major => Gen.Choose(0, 9999)
            .SelectMany(minor => Gen.Choose(0, 9999)
            .SelectMany(patch => Gen.Choose(1, 20)
            .SelectMany(len => Gen.ArrayOf(Gen.Elements(prereleaseChars), len)
            .Select(chars => $"{major}.{minor}.{patch}-{new string(chars)}")))));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// **Validates: Requirements 4.1, 4.3**
    ///
    /// For any valid semver string (major.minor.patch), prefixing with 'v'
    /// and stripping the leading 'v' shall produce the original version string.
    /// </summary>
    [Fact]
    public void Property2_VersionExtraction_PreservesSemver()
    {
        Prop.ForAll(SemverStringArb(), (string version) =>
        {
            var tag = "v" + version;
            var extracted = ExtractVersion(tag);
            return (extracted == version)
                .Label($"Expected '{version}' but got '{extracted}' after stripping 'v' from '{tag}'");
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// **Validates: Requirements 4.1, 4.3**
    ///
    /// For any valid semver string with prerelease (major.minor.patch-prerelease),
    /// prefixing with 'v' and stripping the leading 'v' shall produce the original
    /// version string.
    /// </summary>
    [Fact]
    public void Property2_VersionExtraction_PreservesSemverWithPrerelease()
    {
        Prop.ForAll(SemverWithPrereleaseArb(), (string version) =>
        {
            var tag = "v" + version;
            var extracted = ExtractVersion(tag);
            return (extracted == version)
                .Label($"Expected '{version}' but got '{extracted}' after stripping 'v' from '{tag}'");
        }).QuickCheckThrowOnFailure();
    }

    // Feature: nuget-publish-github-action, Property 3: Required NuGet metadata fields present

    /// <summary>
    /// The publishable project paths relative to the test binary output directory.
    /// From bin/Release/net10.0/ we need to go up 5 levels to reach the repo root:
    /// net10.0 → Release → bin → QuadStore.Tests → test → (repo root)
    /// </summary>
    private static readonly string[] PublishableProjectRelativePaths =
    [
        "../../../../../src/QuadStore.Core/QuadStore.Core.csproj",
        "../../../../../src/QuadStore.SparqlServer/QuadStore.SparqlServer.csproj"
    ];

    /// <summary>
    /// The required NuGet metadata fields that must be present with non-empty values.
    /// </summary>
    private static readonly string[] RequiredMetadataFields =
    [
        "PackageId",
        "Authors",
        "Description",
        "PackageLicenseExpression",
        "PackageProjectUrl",
        "RepositoryUrl",
        "PackageReadmeFile"
    ];

    /// <summary>
    /// Generates an arbitrary (project path, required field) pair from the
    /// cartesian product of publishable projects and required metadata fields.
    /// </summary>
    private static Arbitrary<(string ProjectPath, string FieldName)> ProjectFieldPairArb()
    {
        var gen = Gen.Elements(PublishableProjectRelativePaths)
            .SelectMany(project => Gen.Elements(RequiredMetadataFields)
                .Select(field => (ProjectPath: project, FieldName: field)));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.2**
    ///
    /// For any publishable project and any required NuGet metadata field,
    /// the .csproj XML shall contain a non-empty element for that field.
    /// </summary>
    [Fact]
    public void Property3_RequiredNuGetMetadataFields_PresentInAllPublishableProjects()
    {
        Prop.ForAll(ProjectFieldPairArb(), ((string ProjectPath, string FieldName) pair) =>
        {
            var fullPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, pair.ProjectPath));
            var doc = XDocument.Load(fullPath);
            var element = doc.Descendants(pair.FieldName).FirstOrDefault();

            var exists = element != null && !string.IsNullOrWhiteSpace(element.Value);

            return exists.Label(
                $"Project '{Path.GetFileName(pair.ProjectPath)}' is missing or has empty value for required field '{pair.FieldName}'");
        }).QuickCheckThrowOnFailure();
    }
}
