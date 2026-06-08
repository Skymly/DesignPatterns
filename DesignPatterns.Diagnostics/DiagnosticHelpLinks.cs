namespace DesignPatterns.Diagnostics;

/// <summary>
/// Help link URIs for <c>DP###</c> diagnostics (DesignPatterns.Docs site).
/// </summary>
public static class DiagnosticHelpLinks
{
    /// <summary>Base URL for the diagnostics reference page.</summary>
    public const string DiagnosticsPage = "https://skymly.github.io/DesignPatterns.Docs/diagnostics";

    /// <summary>Returns the help link for a diagnostic id (e.g. <c>DP003</c>).</summary>
    public static string For(string diagnosticId) =>
        $"{DiagnosticsPage}#{diagnosticId.ToLowerInvariant()}";
}
