using DesignPatterns.Analyzers;
using DesignPatterns.Diagnostics;

namespace DesignPatterns.Analyzers.Tests;

public sealed class DesignPatternsDiagnosticDescriptorsTests
{
    [Theory]
    [InlineData("DP001")]
    [InlineData("DP006")]
    [InlineData("DP024")]
    [InlineData("DP025")]
    public void Help_link_uses_diagnostics_page_fragment(string diagnosticId)
    {
        var helpLink = DiagnosticHelpLinks.For(diagnosticId);

        Assert.Equal($"https://skymly.github.io/DesignPatterns.Docs/diagnostics#{diagnosticId.ToLowerInvariant()}", helpLink);
    }

    [Fact]
    public void Analyzer_descriptor_exposes_help_link_and_description()
    {
        var descriptor = new UnregisteredStrategyAnalyzer().SupportedDiagnostics[0];

        Assert.Equal(DiagnosticIds.RegisterStrategyUnregisteredImplementation, descriptor.Id);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.Contains("[RegisterStrategy]", descriptor.MessageFormat.ToString());
    }

    [Fact]
    public void Registry_key_descriptor_exposes_help_link_and_description()
    {
        var descriptor = new UnknownRegistryKeyAnalyzer().SupportedDiagnostics[0];

        Assert.Equal(DiagnosticIds.RegistryKeyNotRegistered, descriptor.Id);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.Contains("Registered keys", descriptor.MessageFormat.ToString());
    }
}
