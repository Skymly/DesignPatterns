using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Tests;

/// <summary>
/// Keeps <see cref="DiagnosticIds"/>, <see cref="DesignPatternsDiagnosticDescriptors"/>,
/// and AnalyzerReleases markdown rows in lockstep (issue #318).
/// </summary>
public sealed class DiagnosticCatalogConsistencyTests
{
    private static readonly ImmutableArray<DiagnosticDescriptor> Catalog = BuildCatalog();
    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> CatalogById =
        Catalog.ToImmutableDictionary(d => d.Id, StringComparer.Ordinal);

    private static readonly ImmutableHashSet<string> DiagnosticIdConstants = BuildDiagnosticIdConstants();

    private static readonly ImmutableArray<AnalyzerReleaseRow> SourceGeneratorsUnshipped =
        ParseEmbedded("AnalyzerReleases.SourceGenerators.Unshipped.md");

    private static readonly ImmutableArray<AnalyzerReleaseRow> AnalyzersShipped =
        ParseEmbedded("AnalyzerReleases.Analyzers.Shipped.md");

    private static readonly ImmutableArray<AnalyzerReleaseRow> AnalyzersUnshipped =
        ParseEmbedded("AnalyzerReleases.Analyzers.Unshipped.md");

    public static TheoryData<string> CatalogIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var id in Catalog.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal))
            {
                data.Add(id);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CatalogIds))]
    public void Descriptor_has_exactly_one_SourceGenerators_Unshipped_row_with_matching_category_and_severity(string id)
    {
        var descriptor = CatalogById[id];
        var matches = SourceGeneratorsUnshipped.Where(r => r.Id == id).ToArray();

        Assert.True(
            matches.Length == 1,
            FormattableString.Invariant(
                $"Expected exactly one SourceGenerators Unshipped row for {id}, found {matches.Length}."));

        var row = matches[0];
        Assert.Equal(descriptor.Category, row.Category);
        Assert.Equal(descriptor.DefaultSeverity, row.Severity);
    }

    [Theory]
    [MemberData(nameof(CatalogIds))]
    public void Descriptor_HelpLinkUri_matches_DiagnosticHelpLinks(string id)
    {
        var descriptor = CatalogById[id];
        Assert.Equal(DiagnosticHelpLinks.For(id), descriptor.HelpLinkUri);
    }

    [Theory]
    [MemberData(nameof(CatalogIds))]
    public void Descriptor_MessageFormat_and_Description_are_non_empty(string id)
    {
        var descriptor = CatalogById[id];
        Assert.False(string.IsNullOrWhiteSpace(descriptor.MessageFormat.ToString()));
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description?.ToString()));
    }

    [Fact]
    public void DiagnosticIds_and_descriptors_are_one_to_one()
    {
        var descriptorIds = CatalogById.Keys.ToImmutableHashSet(StringComparer.Ordinal);
        var missingDescriptors = DiagnosticIdConstants.Except(descriptorIds).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var missingConstants = descriptorIds.Except(DiagnosticIdConstants).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.True(
            missingDescriptors.Length == 0 && missingConstants.Length == 0,
            "DiagnosticIds ↔ descriptors mismatch. " +
            $"Ids without descriptor: [{string.Join(", ", missingDescriptors)}]. " +
            $"Descriptors without DiagnosticIds constant: [{string.Join(", ", missingConstants)}].");
    }

    [Fact]
    public void No_id_is_declared_by_more_than_one_descriptor()
    {
        var duplicates = Catalog
            .GroupBy(d => d.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            $"Duplicate descriptor ids: [{string.Join(", ", duplicates)}].");
    }

    [Fact]
    public void SourceGenerators_Unshipped_rows_reference_only_catalog_ids()
    {
        var unknown = SourceGeneratorsUnshipped
            .Select(r => r.Id)
            .Where(id => !CatalogById.ContainsKey(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unknown.Length == 0,
            $"SourceGenerators Unshipped rows not in catalog: [{string.Join(", ", unknown)}].");
    }

    [Fact]
    public void Analyzers_Shipped_and_Unshipped_rows_are_subset_of_catalog_with_matching_category_and_severity()
    {
        AssertRowsAreSubsetOfCatalog(AnalyzersShipped, "Analyzers Shipped");
        AssertRowsAreSubsetOfCatalog(AnalyzersUnshipped, "Analyzers Unshipped");
    }

    [Fact]
    public void No_id_appears_in_both_Analyzers_Shipped_and_Unshipped()
    {
        var shipped = AnalyzersShipped.Select(r => r.Id).ToImmutableHashSet(StringComparer.Ordinal);
        var overlap = AnalyzersUnshipped
            .Select(r => r.Id)
            .Where(shipped.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            overlap.Length == 0,
            $"Ids in both Analyzers Shipped and Unshipped: [{string.Join(", ", overlap)}].");
    }

    private static void AssertRowsAreSubsetOfCatalog(ImmutableArray<AnalyzerReleaseRow> rows, string label)
    {
        var failures = new List<string>();
        foreach (var row in rows)
        {
            if (!CatalogById.TryGetValue(row.Id, out var descriptor))
            {
                failures.Add(FormattableString.Invariant($"{row.Id}: not in catalog"));
                continue;
            }

            if (!string.Equals(descriptor.Category, row.Category, StringComparison.Ordinal))
            {
                failures.Add(FormattableString.Invariant(
                    $"{row.Id}: category markdown '{row.Category}' != descriptor '{descriptor.Category}'"));
            }

            if (descriptor.DefaultSeverity != row.Severity)
            {
                failures.Add(FormattableString.Invariant(
                    $"{row.Id}: severity markdown '{row.Severity}' != descriptor '{descriptor.DefaultSeverity}'"));
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{label} subset/match failures: {string.Join("; ", failures)}");
    }

    private static ImmutableArray<DiagnosticDescriptor> BuildCatalog()
    {
        var descriptors = new List<DiagnosticDescriptor>();
        var type = typeof(DesignPatternsDiagnosticDescriptors);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.PropertyType == typeof(DiagnosticDescriptor))
            {
                descriptors.Add((DiagnosticDescriptor)property.GetValue(null)!);
                continue;
            }

            if (property.PropertyType == typeof(KeyedRegistrationDiagnostics))
            {
                var group = (KeyedRegistrationDiagnostics)property.GetValue(null)!;
                descriptors.Add(group.DuplicateKey);
                descriptors.Add(group.ContractMismatch);
                descriptors.Add(group.MissingParameterlessConstructor);
            }
        }

        return descriptors.ToImmutableArray();
    }

    private static ImmutableHashSet<string> BuildDiagnosticIdConstants()
    {
        return typeof(DiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    private static ImmutableArray<AnalyzerReleaseRow> ParseEmbedded(string resourceName)
    {
        var assembly = typeof(DiagnosticCatalogConsistencyTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd();
        return ParseAnalyzerReleasesMarkdown(text);
    }

    private static ImmutableArray<AnalyzerReleaseRow> ParseAnalyzerReleasesMarkdown(string text)
    {
        var rows = new List<AnalyzerReleaseRow>();
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("DP", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 3)
            {
                continue;
            }

            var id = parts[0].Trim();
            var category = parts[1].Trim();
            var severityText = parts[2].Trim();
            if (!Enum.TryParse<DiagnosticSeverity>(severityText, ignoreCase: true, out var severity))
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Unrecognized severity '{severityText}' for {id} in AnalyzerReleases markdown."));
            }

            rows.Add(new AnalyzerReleaseRow(id, category, severity));
        }

        return rows.ToImmutableArray();
    }

    private readonly record struct AnalyzerReleaseRow(string Id, string Category, DiagnosticSeverity Severity);
}
