using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Immutable event-type-level metadata extracted from <c>[RegisterEventHandler]</c> attributes.
/// Used as the grouping key in <see cref="RegisterEventHandlerGenerator"/>.
/// </summary>
internal sealed record EventInfo(
    string FullyQualifiedName,
    string Name,
    string? Namespace,
    string FullyQualifiedDisplayString);

/// <summary>
/// Immutable handler registration model collected by the incremental pipeline.
/// All fields are value-equatable to ensure correct incremental caching.
/// </summary>
internal sealed record EventHandlerRegistration(
    EventInfo Event,
    string HandlerName,
    string HandlerFullyQualifiedDisplayString,
    bool ImplementsHandlerInterface,
    bool HasPublicParameterlessConstructor,
    Location Location);

/// <summary>
/// Generates <c>{Event}EventHandlerRegistry</c> static classes for
/// <c>[RegisterEventHandler]</c>-attributed handler implementations.
/// Each registry exposes <c>SubscribeAll(IEventAggregator)</c> and, when DI
/// integration is enabled, <c>RegisterDi(IServiceCollection)</c> plus
/// <c>SubscribeAll(IEventAggregator, IServiceProvider)</c>.
/// </summary>
[Generator]
public sealed class RegisterEventHandlerGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for non-generic <c>RegisterEventHandlerAttribute</c>.</summary>
    public const string RegisterEventHandlerMetadataName = "DesignPatterns.Behavioral.RegisterEventHandlerAttribute";

    /// <summary>Metadata name for generic <c>RegisterEventHandlerAttribute&lt;TEvent&gt;</c>.</summary>
    public const string RegisterEventHandlerGenericMetadataName = "DesignPatterns.Behavioral.RegisterEventHandlerAttribute`1";

    private const string HandlerInterfaceMetadataName = "DesignPatterns.Behavioral.IEventHandler`1";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterEventHandlerMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false))
            .WithTrackingName(TrackingNames.EventHandlerNonGenericTransform);

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterEventHandlerGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true))
            .WithTrackingName(TrackingNames.EventHandlerGenericTransform);

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()).Combine(integrationOptions),
            (spc, source) => Execute(
                spc,
                source.Left.Left.SelectMany(static list => list).ToImmutableArray(),
                source.Left.Right.SelectMany(static list => list).ToImmutableArray(),
                source.Right));
    }

    private static List<EventHandlerRegistration> Transform(
        GeneratorAttributeSyntaxContext context,
        bool isGenericAttribute)
    {
        var result = new List<EventHandlerRegistration>();

        if (context.TargetSymbol is not INamedTypeSymbol handler)
        {
            return result;
        }

        foreach (var attribute in context.Attributes)
        {
            INamedTypeSymbol? eventType = null;
            if (isGenericAttribute)
            {
                if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 })
                {
                    eventType = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                }
            }
            else if (attribute.ConstructorArguments.Length > 0)
            {
                eventType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            }

            if (eventType is null || eventType.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var eventInfo = new EventInfo(
                eventType.ToDisplayString(),
                eventType.Name,
                eventType.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : eventType.ContainingNamespace.ToDisplayString(),
                eventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            var location = context.TargetNode.GetLocation();
            result.Add(new EventHandlerRegistration(
                eventInfo,
                handler.Name,
                handler.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ImplementsHandlerInterface(handler, eventType),
                HasPublicParameterlessConstructor(handler),
                location));
        }

        return result;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<EventHandlerRegistration> nonGeneric,
        ImmutableArray<EventHandlerRegistration> generic,
        GeneratorIntegrationOptions integrationOptions)
    {
        var registrations = nonGeneric.Concat(generic).ToList();
        if (registrations.Count == 0)
        {
            return;
        }

        // Report DP046 (contract mismatch) for handlers that do not implement IEventHandler<TEvent>.
        foreach (var registration in registrations.Where(static r => !r.ImplementsHandlerInterface))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.RegisterEventHandlerContractMismatch,
                registration.Location,
                registration.HandlerName,
                registration.Event.FullyQualifiedName));
        }

        // Report DP045 (duplicate on same class for same event type).
        foreach (var g in registrations.GroupBy(static r => (r.HandlerFullyQualifiedDisplayString, r.Event.FullyQualifiedName)))
        {
            if (g.Count() <= 1)
            {
                continue;
            }

            foreach (var registration in g)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.RegisterEventHandlerDuplicateOnSameClass,
                    registration.Location,
                    registration.HandlerName,
                    registration.Event.FullyQualifiedName));
            }
        }

        // Detect event type name collisions for HintName qualification.
        var eventNamesWithCollisions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in registrations.GroupBy(static r => r.Event.Name, StringComparer.Ordinal))
        {
            var distinctFqns = g.Select(static r => r.Event.FullyQualifiedName).Distinct(StringComparer.Ordinal).ToList();
            if (distinctFqns.Count > 1)
            {
                eventNamesWithCollisions.Add(g.Key);
            }
        }

        // Group by event type and emit registries for valid handlers.
        var valid = registrations
            .Where(static r => r.ImplementsHandlerInterface)
            .GroupBy(static r => r.Event.FullyQualifiedName, StringComparer.Ordinal);

        foreach (var group in valid)
        {
            var eventInfo = group.First().Event;
            // Deduplicate by handler type (a handler may appear once per event type even if
            // both generic and non-generic attributes were applied — that is a duplicate
            // reported above, but we still emit only one entry in the registry).
            var distinctHandlers = group
                .GroupBy(static r => r.HandlerFullyQualifiedDisplayString, StringComparer.Ordinal)
                .Select(static g => g.First())
                .OrderBy(static r => r.HandlerFullyQualifiedDisplayString, StringComparer.Ordinal)
                .ToList();

            if (distinctHandlers.Count == 0)
            {
                continue;
            }

            // Static SubscribeAll path only includes handlers with a public parameterless constructor.
            var staticHandlerTypeNames = distinctHandlers
                .Where(static r => r.HasPublicParameterlessConstructor)
                .Select(static r => r.HandlerFullyQualifiedDisplayString)
                .ToList();

            // DI SubscribeAll path includes all valid handlers.
            var diHandlerTypeNames = distinctHandlers
                .Select(static r => r.HandlerFullyQualifiedDisplayString)
                .ToList();

            EmitRegistry(
                context,
                eventInfo,
                staticHandlerTypeNames,
                diHandlerTypeNames,
                integrationOptions,
                eventNamesWithCollisions.Contains(eventInfo.Name));
        }
    }

    private static void EmitRegistry(
        SourceProductionContext context,
        EventInfo eventInfo,
        IReadOnlyList<string> staticHandlerTypeNames,
        IReadOnlyList<string> diHandlerTypeNames,
        GeneratorIntegrationOptions integrationOptions,
        bool qualifyHintName)
    {
        var registryClassName = EventAggregatorSyntaxFactory.GetHandlerRegistryClassName(eventInfo.Name);

        var registryUnit = EventAggregatorSyntaxFactory.CreateHandlerRegistryCompilationUnit(
            eventInfo.Namespace,
            registryClassName,
            eventInfo.FullyQualifiedDisplayString,
            staticHandlerTypeNames,
            diHandlerTypeNames,
            integrationOptions);

        var hintPrefix = qualifyHintName
            ? HintNameHelper.FromString(eventInfo.FullyQualifiedDisplayString)
            : HintNameHelper.FromString(eventInfo.Name);
        context.AddSource(
            $"{hintPrefix}.{registryClassName}.g.cs",
            SourceText.From(registryUnit.ToFullString(), Encoding.UTF8));
    }

    private static bool ImplementsHandlerInterface(INamedTypeSymbol handler, INamedTypeSymbol eventType)
    {
        foreach (var iface in handler.AllInterfaces)
        {
            if (iface.MetadataName == "IEventHandler`1" &&
                iface.ContainingNamespace.ToDisplayString() == "DesignPatterns.Behavioral" &&
                iface.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], eventType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol implementation) =>
        implementation.InstanceConstructors.Any(static c =>
            c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);
}
