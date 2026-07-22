using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers.Di;

/// <summary>
/// Collects DesignPatterns attributed implementation types and matches
/// them to <c>RegisterDi</c> holder type names by registration category.
/// Heuristics are frozen as used by Captive Dependency DP062.
/// </summary>
internal static class AttributedRegistration
{
    /// <summary>
    /// Scans all types in the compilation for DesignPatterns registration attributes
    /// and groups them by category for matching to the correct <c>RegisterDi</c> call.
    /// </summary>
    internal static Dictionary<RegistrationCategory, HashSet<INamedTypeSymbol>> CollectByCategory(
        Compilation compilation)
    {
        var result = new Dictionary<RegistrationCategory, HashSet<INamedTypeSymbol>>
        {
            [RegistrationCategory.Strategy] = new(SymbolEqualityComparer.Default),
            [RegistrationCategory.Factory] = new(SymbolEqualityComparer.Default),
            [RegistrationCategory.EventHandler] = new(SymbolEqualityComparer.Default),
            [RegistrationCategory.Decorator] = new(SymbolEqualityComparer.Default),
            [RegistrationCategory.Composite] = new(SymbolEqualityComparer.Default),
        };

        foreach (var assembly in AnalyzerSymbolHelper.GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(assembly.GlobalNamespace))
            {
                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    var attrName = attribute.AttributeClass?.ToDisplayString();
                    if (attrName is null)
                    {
                        continue;
                    }

                    var category = attrName switch
                    {
                        _ when attrName == StrategyAnalysisConstants.RegisterStrategyMetadataName =>
                            RegistrationCategory.Strategy,
                        _ when attrName == FactoryAnalysisConstants.RegisterFactoryMetadataName =>
                            RegistrationCategory.Factory,
                        _ when attrName == EventHandlerAnalysisConstants.RegisterEventHandlerMetadataName =>
                            RegistrationCategory.EventHandler,
                        "DesignPatterns.Structural.DecoratorAttribute" =>
                            RegistrationCategory.Decorator,
                        "DesignPatterns.Structural.CompositePartAttribute" =>
                            RegistrationCategory.Composite,
                        _ => (RegistrationCategory?)null,
                    };

                    if (category is { } cat)
                    {
                        result[cat].Add(typeSymbol);
                        break;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Maps a <c>RegisterDi</c> holder type name to its registration category
    /// based on naming conventions used by the source generators.
    /// </summary>
    internal static RegistrationCategory? MatchCategoryByHolderName(string holderTypeName)
    {
        if (holderTypeName.Contains("Strategy"))
        {
            return RegistrationCategory.Strategy;
        }

        if (holderTypeName.Contains("Factory"))
        {
            return RegistrationCategory.Factory;
        }

        if (holderTypeName.Contains("EventHandler") || holderTypeName.Contains("EventAggregator"))
        {
            return RegistrationCategory.EventHandler;
        }

        if (holderTypeName.Contains("Decorator"))
        {
            return RegistrationCategory.Decorator;
        }

        if (holderTypeName.Contains("Composite"))
        {
            return RegistrationCategory.Composite;
        }

        return null;
    }

    /// <summary>
    /// Default implementation lifetime when the call omits an explicit argument:
    /// Factory holders → Transient; all others → Singleton.
    /// </summary>
    internal static Lifetime DefaultLifetimeForHolder(string holderTypeName) =>
        holderTypeName.Contains("Factory") ? Lifetime.Transient : Lifetime.Singleton;
}
