using System.Collections.Immutable;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete factory implementations that implement a registered contract but lack <c>[RegisterFactory]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredFactoryAnalyzer : UnregisteredContractRegistrationAnalyzerBase
{
    private static readonly DiagnosticDescriptor RuleDefinition = new(
        DiagnosticIds.RegisterFactoryUnregisteredImplementation,
        title: "Factory implementation is not registered",
        messageFormat: "Type '{0}' implements factory contract '{1}' but is missing [RegisterFactory]",
        category: "DesignPatterns.Analyzers",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    protected override DiagnosticDescriptor Rule => RuleDefinition;

    protected override bool IsRegistrationAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.ToDisplayString() == FactoryAnalysisConstants.RegisterFactoryMetadataName ||
               attributeClass.OriginalDefinition.ToDisplayString() == FactoryAnalysisConstants.RegisterFactoryGenericMetadataName;
    }
}
