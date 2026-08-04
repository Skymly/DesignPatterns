using System.Collections.Generic;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete event handler implementations that implement <c>IEventHandler&lt;TEvent&gt;</c>
/// for a registered event type but lack <c>[RegisterEventHandler]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredEventHandlerAnalyzer : UnregisteredPayloadPeerAnalyzerBase
{
    private static readonly DiagnosticDescriptor RuleDefinition =
        DesignPatternsDiagnosticDescriptors.EventHandlerUnregisteredImplementation;

    protected override DiagnosticDescriptor Rule => RuleDefinition;

    protected override IEnumerable<INamedTypeSymbol> GetPeersFromRegistrationAttributes(
        INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!EventHandlerAnalysisConstants.IsRegisterEventHandlerAttribute(attribute.AttributeClass))
            {
                continue;
            }

            var eventType = TryGetEventTypeFromAttribute(attribute);
            if (eventType is not null)
            {
                yield return eventType;
            }
        }
    }

    protected override IEnumerable<INamedTypeSymbol> GetDeclaredPeers(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name != "IEventHandler" || iface.TypeArguments.Length != 1)
            {
                continue;
            }

            if (iface.ContainingNamespace.ToDisplayString() != "DesignPatterns.Behavioral")
            {
                continue;
            }

            if (iface.TypeArguments[0] is INamedTypeSymbol eventType)
            {
                yield return eventType;
            }
        }
    }

    private static INamedTypeSymbol? TryGetEventTypeFromAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass?.IsGenericType == true)
        {
            return attribute.AttributeClass.TypeArguments.Length == 1
                ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
                : null;
        }

        if (attribute.ConstructorArguments.Length >= 1 &&
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Type &&
            attribute.ConstructorArguments[0].Value is INamedTypeSymbol nonGenericEvent)
        {
            return nonGenericEvent;
        }

        return null;
    }
}
