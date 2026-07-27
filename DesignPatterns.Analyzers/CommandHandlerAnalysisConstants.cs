using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers;

internal static class CommandHandlerAnalysisConstants
{
    internal const string RegisterCommandHandlerMetadataName = "DesignPatterns.Behavioral.RegisterCommandHandlerAttribute";
    internal const string RegisterCommandHandlerGenericMetadataName = "DesignPatterns.Behavioral.RegisterCommandHandlerAttribute`1";

    internal static bool IsRegisterCommandHandlerAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.OriginalDefinition.MetadataName switch
        {
            "RegisterCommandHandlerAttribute" or "RegisterCommandHandlerAttribute`1" => true,
            _ => false,
        };
    }
}
