using System.Collections.Generic;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete handler implementations that implement <c>IHandler&lt;TContext&gt;</c>
/// for a registered context but lack <c>[HandlerOrder]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredHandlerAnalyzer : UnregisteredPayloadPeerAnalyzerBase
{
    private static readonly DiagnosticDescriptor RuleDefinition =
        DesignPatternsDiagnosticDescriptors.HandlerOrderUnregisteredImplementation;

    protected override DiagnosticDescriptor Rule => RuleDefinition;

    protected override IEnumerable<INamedTypeSymbol> GetPeersFromRegistrationAttributes(
        INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsHandlerOrderAttribute(attribute.AttributeClass))
            {
                continue;
            }

            var context = TryGetContextFromAttribute(attribute);
            if (context is not null)
            {
                yield return context;
            }
        }
    }

    protected override IEnumerable<INamedTypeSymbol> GetDeclaredPeers(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name != "IHandler" || iface.TypeArguments.Length != 1)
            {
                continue;
            }

            if (iface.TypeArguments[0] is INamedTypeSymbol contextType)
            {
                yield return contextType;
            }
        }
    }

    private static bool IsHandlerOrderAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        var metadataName = attributeClass.MetadataName;
        if (metadataName == "HandlerOrderAttribute")
        {
            return true;
        }

        return attributeClass.OriginalDefinition.MetadataName == "HandlerOrderAttribute`1";
    }

    private static INamedTypeSymbol? TryGetContextFromAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass?.IsGenericType == true)
        {
            return attribute.AttributeClass.TypeArguments.Length == 1
                ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
                : null;
        }

        if (attribute.ConstructorArguments.Length >= 2 &&
            attribute.ConstructorArguments[1].Kind == TypedConstantKind.Type &&
            attribute.ConstructorArguments[1].Value is INamedTypeSymbol nonGenericContext)
        {
            return nonGenericContext;
        }

        return null;
    }
}
