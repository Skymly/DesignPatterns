using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers;

internal static class StrategyAnalysisConstants
{
    internal const string RegisterStrategyMetadataName = "DesignPatterns.Behavioral.RegisterStrategyAttribute";
    internal const string RegisterStrategyGenericMetadataName = "DesignPatterns.Behavioral.RegisterStrategyAttribute`1";

    internal static bool IsRegisterStrategyAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.OriginalDefinition.MetadataName switch
        {
            "RegisterStrategyAttribute" or "RegisterStrategyAttribute`1" => true,
            _ => false,
        };
    }
}
