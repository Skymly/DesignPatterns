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
    private static readonly DiagnosticDescriptor RuleDefinition =
        DesignPatternsDiagnosticDescriptors.RegisterStrategyUnregisteredImplementation;

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
