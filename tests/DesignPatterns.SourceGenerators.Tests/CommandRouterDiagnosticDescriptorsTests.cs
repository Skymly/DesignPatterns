using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Tests;

/// <summary>
/// Seam: public DiagnosticIds + DesignPatternsDiagnosticDescriptors for Command Router (issue #257).
/// No analyzer/generator behavior is exercised here — only stable DP### identities and descriptor contracts.
/// </summary>
public sealed class CommandRouterDiagnosticDescriptorsTests
{
    [Fact]
    public void Command_router_diagnostic_ids_are_allocated_after_adr008_reservation()
    {
        Assert.Equal("DP072", DiagnosticIds.CommandHandlerUnregisteredImplementation);
        Assert.Equal("DP073", DiagnosticIds.RegisterCommandHandlerDuplicateCommand);
        Assert.Equal("DP074", DiagnosticIds.RegisterCommandHandlerContractMismatch);

        Assert.Equal("DP067", DiagnosticIds.GenerateSingletonInitializeAsyncInvalid);
        Assert.Equal("DP071", DiagnosticIds.StaticMutableSingletonDiDoubleRegistration);
    }

    [Fact]
    public void Unregistered_command_handler_descriptor_is_analyzer_info_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.CommandHandlerUnregisteredImplementation;

        Assert.Equal(DiagnosticIds.CommandHandlerUnregisteredImplementation, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Analyzers", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("[RegisterCommandHandler]", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("Add", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_command_handler_descriptor_is_generator_error_with_colliding_handlers()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.RegisterCommandHandlerDuplicateCommand;

        Assert.Equal(DiagnosticIds.RegisterCommandHandlerDuplicateCommand, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("[RegisterCommandHandler]", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{1}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("{2}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Contract_mismatch_descriptor_is_generator_error_with_actionable_message()
    {
        var descriptor = DesignPatternsDiagnosticDescriptors.RegisterCommandHandlerContractMismatch;

        Assert.Equal(DiagnosticIds.RegisterCommandHandlerContractMismatch, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal("DesignPatterns.Generators", descriptor.Category);
        Assert.Equal(DiagnosticHelpLinks.For(descriptor.Id), descriptor.HelpLinkUri);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()));
        Assert.Contains("ICommandHandler", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
        Assert.Contains("[RegisterCommandHandler]", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }
}
