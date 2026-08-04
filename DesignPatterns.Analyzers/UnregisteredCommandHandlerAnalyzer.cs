using System.Collections.Generic;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete command handler implementations that implement <c>ICommandHandler&lt;TCommand&gt;</c>
/// (or <c>ICommandHandler&lt;TCommand, TResult&gt;</c>) for a registered command type but lack
/// <c>[RegisterCommandHandler]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredCommandHandlerAnalyzer : UnregisteredPayloadPeerAnalyzerBase
{
    private static readonly DiagnosticDescriptor RuleDefinition =
        DesignPatternsDiagnosticDescriptors.CommandHandlerUnregisteredImplementation;

    protected override DiagnosticDescriptor Rule => RuleDefinition;

    protected override IEnumerable<INamedTypeSymbol> GetPeersFromRegistrationAttributes(
        INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!CommandHandlerAnalysisConstants.IsRegisterCommandHandlerAttribute(attribute.AttributeClass))
            {
                continue;
            }

            var commandType = TryGetCommandTypeFromAttribute(attribute);
            if (commandType is not null)
            {
                yield return commandType;
            }
        }
    }

    protected override IEnumerable<INamedTypeSymbol> GetDeclaredPeers(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name != "ICommandHandler")
            {
                continue;
            }

            if (iface.TypeArguments.Length is not (1 or 2))
            {
                continue;
            }

            if (iface.ContainingNamespace.ToDisplayString() != "DesignPatterns.Behavioral")
            {
                continue;
            }

            if (iface.TypeArguments[0] is INamedTypeSymbol commandType)
            {
                yield return commandType;
            }
        }
    }

    private static INamedTypeSymbol? TryGetCommandTypeFromAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass?.IsGenericType == true)
        {
            return attribute.AttributeClass.TypeArguments.Length == 1
                ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
                : null;
        }

        if (attribute.ConstructorArguments.Length >= 1 &&
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Type &&
            attribute.ConstructorArguments[0].Value is INamedTypeSymbol nonGenericCommand)
        {
            return nonGenericCommand;
        }

        return null;
    }
}
