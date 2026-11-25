using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Tests;

/// <summary>
/// Seam: public DiagnosticIds + DesignPatternsDiagnosticDescriptors for Step Builder
/// (issue #289). No generator behavior is exercised here — only stable DP### identities
/// and descriptor contracts for the upcoming GenerateBuilderGenerator.
/// </summary>
public sealed class StepBuilderDiagnosticDescriptorsTests
{
    [Fact]
    public void Step_builder_diagnostic_ids_are_allocated_after_command_router_range()
    {
        Assert.Equal("DP078", DiagnosticIds.GenerateBuilderRequiredStepCapExceeded);
        Assert.Equal("DP079", DiagnosticIds.GenerateBuilderMissingAssemble);
        Assert.Equal("DP080", DiagnosticIds.GenerateBuilderAssembleParameterMismatch);
        Assert.Equal("DP081", DiagnosticIds.GenerateBuilderMutexConflict);
        Assert.Equal("DP082", DiagnosticIds.GenerateBuilderPartialOrderViolation);
        Assert.Equal("DP083", DiagnosticIds.GenerateBuilderDuplicateStep);
        Assert.Equal("DP084", DiagnosticIds.GenerateBuilderUnknownStepReference);
        Assert.Equal("DP085", DiagnosticIds.GenerateBuilderInvalidHolder);
        Assert.Equal("DP086", DiagnosticIds.GenerateBuilderAssembleContractMismatch);

        Assert.Equal("DP067", DiagnosticIds.GenerateSingletonInitializeAsyncInvalid);
        Assert.Equal("DP071", DiagnosticIds.StaticMutableSingletonDiDoubleRegistration);
        Assert.Equal("DP077", DiagnosticIds.CommandPipelineBehaviorContractMismatch);
    }

    [Fact]
    public void Required_step_cap_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderRequiredStepCapExceeded;

        Assert.Equal(DiagnosticIds.GenerateBuilderRequiredStepCapExceeded, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("8", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_assemble_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderMissingAssemble;

        Assert.Equal(DiagnosticIds.GenerateBuilderMissingAssemble, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("[BuilderAssemble]", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_parameter_mismatch_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderAssembleParameterMismatch;

        Assert.Equal(DiagnosticIds.GenerateBuilderAssembleParameterMismatch, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{1}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Mutex_conflict_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderMutexConflict;

        Assert.Equal(DiagnosticIds.GenerateBuilderMutexConflict, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("mutex", descriptor.MessageFormat.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{1}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Partial_order_violation_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderPartialOrderViolation;

        Assert.Equal(DiagnosticIds.GenerateBuilderPartialOrderViolation, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{1}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_step_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderDuplicateStep;

        Assert.Equal(DiagnosticIds.GenerateBuilderDuplicateStep, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("[BuilderStep]", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_step_reference_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderUnknownStepReference;

        Assert.Equal(DiagnosticIds.GenerateBuilderUnknownStepReference, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{1}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_holder_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderInvalidHolder;

        Assert.Equal(DiagnosticIds.GenerateBuilderInvalidHolder, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("[GenerateBuilder]", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_contract_mismatch_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.GenerateBuilderAssembleContractMismatch;
        var message = descriptor.MessageFormat.ToString();
        var description = descriptor.Description.ToString();

        Assert.Equal(DiagnosticIds.GenerateBuilderAssembleContractMismatch, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Contains("[BuilderAssemble]", message, StringComparison.Ordinal);
        Assert.Contains("{0}", message, StringComparison.Ordinal);
        Assert.Contains("{1}", message, StringComparison.Ordinal);
        Assert.Contains("Task<T>", message, StringComparison.Ordinal);
        Assert.Contains("ValueTask<T>", message, StringComparison.Ordinal);
        Assert.Contains("Task<T>", description, StringComparison.Ordinal);
        Assert.Contains("ValueTask<T>", description, StringComparison.Ordinal);
    }
}
