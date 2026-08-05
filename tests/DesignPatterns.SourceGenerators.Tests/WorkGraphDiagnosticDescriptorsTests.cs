using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Tests;

/// <summary>
/// Seam: public DiagnosticIds + DesignPatternsDiagnosticDescriptors for Work Graph
/// (issue #310 / Spec #308). No generator behavior is exercised here — only stable
/// DP### identities and descriptor contracts for the upcoming Work Graph generator.
/// </summary>
public sealed class WorkGraphDiagnosticDescriptorsTests
{
    [Fact]
    public void Work_graph_diagnostic_ids_are_allocated_from_dp087()
    {
        Assert.Equal("DP087", DiagnosticIds.WorkGraphCycle);
        Assert.Equal("DP088", DiagnosticIds.WorkGraphUnknownDependency);
        Assert.Equal("DP089", DiagnosticIds.WorkGraphDuplicateStepId);
        Assert.Equal("DP090", DiagnosticIds.WorkGraphSelfDependency);
        Assert.Equal("DP091", DiagnosticIds.WorkGraphUnreachableStep);
        Assert.Equal("DP092", DiagnosticIds.WorkGraphContractMismatch);

        Assert.Equal("DP086", DiagnosticIds.GenerateBuilderAssembleContractMismatch);
    }

    [Fact]
    public void Cycle_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.WorkGraphCycle;

        Assert.Equal(DiagnosticIds.WorkGraphCycle, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("cycle", descriptor.MessageFormat.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_dependency_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.WorkGraphUnknownDependency;

        Assert.Equal(DiagnosticIds.WorkGraphUnknownDependency, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("DependsOn", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{1}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_step_id_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.WorkGraphDuplicateStepId;

        Assert.Equal(DiagnosticIds.WorkGraphDuplicateStepId, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("more than once", descriptor.MessageFormat.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Self_dependency_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.WorkGraphSelfDependency;

        Assert.Equal(DiagnosticIds.WorkGraphSelfDependency, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("self", descriptor.MessageFormat.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unreachable_step_descriptor_is_generator_warning_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.WorkGraphUnreachableStep;

        Assert.Equal(DiagnosticIds.WorkGraphUnreachableStep, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("unreachable", descriptor.MessageFormat.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Contract_mismatch_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.WorkGraphContractMismatch;

        Assert.Equal(DiagnosticIds.WorkGraphContractMismatch, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("IWorkStep", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{1}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }
}
