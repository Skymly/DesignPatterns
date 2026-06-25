using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers;

internal static class EventHandlerAnalysisConstants
{
    internal const string RegisterEventHandlerMetadataName = "DesignPatterns.Behavioral.RegisterEventHandlerAttribute";
    internal const string RegisterEventHandlerGenericMetadataName = "DesignPatterns.Behavioral.RegisterEventHandlerAttribute`1";

    internal static bool IsRegisterEventHandlerAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.OriginalDefinition.MetadataName switch
        {
            "RegisterEventHandlerAttribute" or "RegisterEventHandlerAttribute`1" => true,
            _ => false,
        };
    }
}
