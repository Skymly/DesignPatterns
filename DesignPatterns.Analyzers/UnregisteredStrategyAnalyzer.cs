using System.Collections.Immutable;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete strategy implementations that implement a registered contract but lack <c>[RegisterStrategy]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredStrategyAnalyzer : UnregisteredContractRegistrationAnalyzerBase
{
    private static readonly DiagnosticDescriptor RuleDefinition = new(
        DiagnosticIds.RegisterStrategyUnregisteredImplementation,
        title: "Strategy implementation is not registered",
        messageFormat: "Type '{0}' implements strategy contract '{1}' but is missing [RegisterStrategy]",
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

        return attributeClass.ToDisplayString() == StrategyAnalysisConstants.RegisterStrategyMetadataName ||
               attributeClass.OriginalDefinition.ToDisplayString() == StrategyAnalysisConstants.RegisterStrategyGenericMetadataName;
    }
}
