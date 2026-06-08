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
    private static readonly DiagnosticDescriptor RuleDefinition =
        DesignPatternsDiagnosticDescriptors.RegisterFactoryUnregisteredImplementation;

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
