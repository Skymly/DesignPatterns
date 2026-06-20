using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers.Tests;

/// <summary>
/// Snapshot model for a single diagnostic, used by Verify snapshots.
/// </summary>
internal sealed record DiagnosticSnapshot(string Id, string Severity, string Message, string Location);

internal static class AnalyzerVerifyHelper
{
    /// <summary>
    /// Converts diagnostics to a Verify-friendly string representation.
    /// Diagnostics are sorted by ID then source location for deterministic output.
    /// </summary>
    internal static string FormatDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        var snapshots = diagnostics
            .Where(d => d.Location != Location.None)
            .Select(ToSnapshot)
            .OrderBy(d => d.Id, System.StringComparer.Ordinal)
            .ThenBy(d => d.Location, System.StringComparer.Ordinal)
            .ToList();

        if (snapshots.Count == 0)
        {
            return "(no diagnostics)";
        }

        var sb = new StringBuilder();
        foreach (var d in snapshots)
        {
            sb.AppendLine($"{d.Id} [{d.Severity}] {d.Message} @ {d.Location}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Converts diagnostics to a Verify-friendly string, filtering to only
    /// diagnostics with the specified IDs. Useful when a compilation produces
    /// unrelated diagnostics (e.g. generator diagnostics) alongside analyzer
    /// diagnostics.
    /// </summary>
    internal static string FormatDiagnostics(ImmutableArray<Diagnostic> diagnostics, params string[] filterIds)
    {
        var idSet = new System.Collections.Generic.HashSet<string>(filterIds);
        var filtered = diagnostics.Where(d => idSet.Contains(d.Id)).ToImmutableArray();
        return FormatDiagnostics(filtered);
    }

    private static DiagnosticSnapshot ToSnapshot(Diagnostic d)
    {
        var severity = d.Severity switch
        {
            DiagnosticSeverity.Error => "Error",
            DiagnosticSeverity.Warning => "Warning",
            DiagnosticSeverity.Info => "Info",
            DiagnosticSeverity.Hidden => "Hidden",
            _ => d.Severity.ToString(),
        };

        var loc = d.Location.IsInSource
            ? $"{d.Location.SourceTree?.FilePath ?? "?"}:({d.Location.GetLineSpan().StartLinePosition.Line + 1},{d.Location.GetLineSpan().StartLinePosition.Character + 1})"
            : "None";

        return new DiagnosticSnapshot(d.Id, severity, d.GetMessage(), loc);
    }
}
