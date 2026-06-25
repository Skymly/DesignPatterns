using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete event handler implementations that implement <c>IEventHandler&lt;TEvent&gt;</c> for a registered event type but lack <c>[RegisterEventHandler]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredEventHandlerAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DesignPatternsDiagnosticDescriptors.EventHandlerUnregisteredImplementation;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var registeredEventTypes = CollectRegisteredEventTypes(context.Compilation);
        if (registeredEventTypes.IsEmpty)
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => AnalyzeNamedType(symbolContext, registeredEventTypes),
            SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> registeredEventTypes)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsAbstract)
        {
            return;
        }

        if (typeSymbol.DeclaredAccessibility == Accessibility.Private && typeSymbol.ContainingType is not null)
        {
            return;
        }

        foreach (var eventType in GetEventHandlerEventTypes(typeSymbol))
        {
            if (!registeredEventTypes.Contains(eventType))
            {
                continue;
            }

            if (HasRegisterEventHandlerForEventType(typeSymbol, eventType))
            {
                continue;
            }

            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                typeSymbol.Name,
                eventType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static ImmutableHashSet<INamedTypeSymbol> CollectRegisteredEventTypes(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var assembly in AnalyzerSymbolHelper.GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(assembly.GlobalNamespace))
            {
                foreach (var eventType in GetRegisteredEventTypesFromType(typeSymbol))
                {
                    builder.Add(eventType);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static IEnumerable<INamedTypeSymbol> GetEventHandlerEventTypes(INamedTypeSymbol typeSymbol)
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

    private static bool HasRegisterEventHandlerForEventType(INamedTypeSymbol typeSymbol, INamedTypeSymbol eventType) =>
        GetRegisteredEventTypesFromType(typeSymbol).Any(
            registeredEvent => SymbolEqualityComparer.Default.Equals(registeredEvent, eventType));

    private static IEnumerable<INamedTypeSymbol> GetRegisteredEventTypesFromType(INamedTypeSymbol typeSymbol)
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
