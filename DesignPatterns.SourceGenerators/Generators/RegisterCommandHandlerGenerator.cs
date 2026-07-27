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
/// Immutable command-type-level metadata extracted from <c>[RegisterCommandHandler]</c> attributes.
/// </summary>
internal sealed record CommandInfo(
    string FullyQualifiedName,
    string Name,
    string? Namespace,
    string FullyQualifiedDisplayString);

/// <summary>
/// Immutable handler registration model collected by the incremental pipeline.
/// </summary>
internal sealed record CommandHandlerRegistration(
    CommandInfo Command,
    string HandlerName,
    string HandlerFullyQualifiedDisplayString,
    bool ImplementsHandlerInterface,
    string? ResultTypeFullyQualifiedDisplayString,
    bool HasPublicParameterlessConstructor,
    LocationInfo Location);

/// <summary>
/// Generates <c>{Command}CommandHandlerRegistry</c> static classes for
/// <c>[RegisterCommandHandler]</c>-attributed handler implementations.
/// Each registry exposes <c>RegisterAll(CommandRouterBuilder)</c> /
/// <c>CreateRouter()</c> for the static path and, when DI integration is enabled,
/// <c>RegisterDi</c> plus provider-based <c>RegisterAll</c>.
/// </summary>
/// <remarks>
/// Ticket ownership (#259): static parameterless-ctor wiring is always emitted.
/// DI / Autofac glue is gated by MSBuild integration flags; packaged
/// <c>AddCommandRouter</c> extensions remain a DependencyInjection / Autofac follow-up.
/// Handlers without a public parameterless constructor are omitted from the static path;
/// when DI/Autofac flags are also off, no registry source is emitted for those handlers
/// (enable <c>DesignPatterns_EnableDiIntegration</c> / Autofac integration to register them).
/// </remarks>
[Generator]
public sealed class RegisterCommandHandlerGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for non-generic <c>RegisterCommandHandlerAttribute</c>.</summary>
    public const string RegisterCommandHandlerMetadataName = "DesignPatterns.Behavioral.RegisterCommandHandlerAttribute";

    /// <summary>Metadata name for generic <c>RegisterCommandHandlerAttribute&lt;TCommand&gt;</c>.</summary>
    public const string RegisterCommandHandlerGenericMetadataName = "DesignPatterns.Behavioral.RegisterCommandHandlerAttribute`1";

    private const string VoidHandlerInterfaceMetadataName = "ICommandHandler`1";
    private const string ResultHandlerInterfaceMetadataName = "ICommandHandler`2";
    private const string HandlerInterfaceNamespace = "DesignPatterns.Behavioral";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterCommandHandlerMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false))
            .WithTrackingName(TrackingNames.CommandHandlerNonGenericTransform);

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterCommandHandlerGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true))
            .WithTrackingName(TrackingNames.CommandHandlerGenericTransform);

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect())
                .WithTrackingName(TrackingNames.CommandHandlerCombine)
                .Combine(integrationOptions)
                .WithTrackingName(TrackingNames.CommandHandlerCombine),
            (spc, source) => Execute(
                spc,
                source.Left.Left.SelectMany(static list => list).ToImmutableArray(),
                source.Left.Right.SelectMany(static list => list).ToImmutableArray(),
                source.Right));
    }

    private static List<CommandHandlerRegistration> Transform(
        GeneratorAttributeSyntaxContext context,
        bool isGenericAttribute)
    {
        var result = new List<CommandHandlerRegistration>();

        if (context.TargetSymbol is not INamedTypeSymbol handler)
        {
            return result;
        }

        foreach (var attribute in context.Attributes)
        {
            INamedTypeSymbol? commandType = null;
            if (isGenericAttribute)
            {
                if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 })
                {
                    commandType = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                }
            }
            else if (attribute.ConstructorArguments.Length > 0)
            {
                commandType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            }

            if (commandType is null || commandType.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var commandInfo = new CommandInfo(
                commandType.ToDisplayString(),
                commandType.Name,
                commandType.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : commandType.ContainingNamespace.ToDisplayString(),
                commandType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            var implements = TryGetHandlerContract(handler, commandType, out var resultTypeDisplay);

            var location = new LocationInfo(context.TargetNode.GetLocation());
            result.Add(new CommandHandlerRegistration(
                commandInfo,
                handler.Name,
                handler.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                implements,
                resultTypeDisplay,
                HasPublicParameterlessConstructor(handler),
                location));
        }

        return result;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<CommandHandlerRegistration> nonGeneric,
        ImmutableArray<CommandHandlerRegistration> generic,
        GeneratorIntegrationOptions integrationOptions)
    {
        var registrations = nonGeneric.Concat(generic).ToList();
        if (registrations.Count == 0)
        {
            return;
        }

        // Report DP074 (contract mismatch) for handlers that do not implement ICommandHandler.
        foreach (var registration in registrations.Where(static r => !r.ImplementsHandlerInterface))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.RegisterCommandHandlerContractMismatch,
                registration.Location.ToLocation(),
                registration.HandlerName,
                registration.Command.FullyQualifiedName));
        }

        // Detect command type name collisions for HintName qualification.
        var commandNamesWithCollisions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in registrations.GroupBy(static r => r.Command.Name, StringComparer.Ordinal))
        {
            var distinctFqns = g.Select(static r => r.Command.FullyQualifiedName).Distinct(StringComparer.Ordinal).ToList();
            if (distinctFqns.Count > 1)
            {
                commandNamesWithCollisions.Add(g.Key);
            }
        }

        // Group by command type. Deduplicate same-handler dual attributes; report DP073 when
        // distinct handlers claim the same command (bijection violation).
        var byCommand = registrations
            .GroupBy(static r => r.Command.FullyQualifiedName, StringComparer.Ordinal);

        foreach (var group in byCommand)
        {
            var distinctHandlers = group
                .GroupBy(static r => r.HandlerFullyQualifiedDisplayString, StringComparer.Ordinal)
                .Select(static g => g.First())
                .OrderBy(static r => r.HandlerFullyQualifiedDisplayString, StringComparer.Ordinal)
                .ToList();

            if (distinctHandlers.Count > 1)
            {
                var first = distinctHandlers[0];
                foreach (var duplicate in distinctHandlers.Skip(1))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DesignPatternsDiagnosticDescriptors.RegisterCommandHandlerDuplicateCommand,
                        duplicate.Location.ToLocation(),
                        duplicate.Command.FullyQualifiedName,
                        first.HandlerName,
                        duplicate.HandlerName));
                }

                // Bijection broken — do not emit registry glue for this command.
                continue;
            }

            var winner = distinctHandlers[0];
            if (!winner.ImplementsHandlerInterface)
            {
                continue;
            }

            var staticHandlerTypeNames = winner.HasPublicParameterlessConstructor
                ? new List<string> { winner.HandlerFullyQualifiedDisplayString }
                : new List<string>();

            var diHandlerTypeNames = new List<string> { winner.HandlerFullyQualifiedDisplayString };

            EmitRegistry(
                context,
                winner.Command,
                winner.ResultTypeFullyQualifiedDisplayString,
                staticHandlerTypeNames,
                diHandlerTypeNames,
                integrationOptions,
                commandNamesWithCollisions.Contains(winner.Command.Name));
        }
    }

    private static void EmitRegistry(
        SourceProductionContext context,
        CommandInfo commandInfo,
        string? resultTypeFullyQualifiedDisplayString,
        IReadOnlyList<string> staticHandlerTypeNames,
        IReadOnlyList<string> diHandlerTypeNames,
        GeneratorIntegrationOptions integrationOptions,
        bool qualifyHintName)
    {
        // Nothing to emit for a DI-only handler when DI/Autofac flags are off.
        if (staticHandlerTypeNames.Count == 0 &&
            !integrationOptions.EnableDi &&
            !integrationOptions.EnableAutofac)
        {
            return;
        }

        var registryClassName = CommandRouterSyntaxFactory.GetHandlerRegistryClassName(commandInfo.Name);

        var registryUnit = CommandRouterSyntaxFactory.CreateHandlerRegistryCompilationUnit(
            commandInfo.Namespace,
            registryClassName,
            commandInfo.FullyQualifiedDisplayString,
            resultTypeFullyQualifiedDisplayString,
            staticHandlerTypeNames,
            diHandlerTypeNames,
            integrationOptions);

        var hintPrefix = qualifyHintName
            ? HintNameHelper.FromString(commandInfo.FullyQualifiedDisplayString)
            : HintNameHelper.FromString(commandInfo.Name);
        context.AddSource(
            $"{hintPrefix}.{registryClassName}.g.cs",
            SourceText.From(registryUnit.ToFullString(), Encoding.UTF8));
    }

    private static bool TryGetHandlerContract(
        INamedTypeSymbol handler,
        INamedTypeSymbol commandType,
        out string? resultTypeFullyQualifiedDisplayString)
    {
        resultTypeFullyQualifiedDisplayString = null;
        INamedTypeSymbol? voidMatch = null;
        INamedTypeSymbol? resultMatch = null;

        foreach (var iface in handler.AllInterfaces)
        {
            if (iface.ContainingNamespace.ToDisplayString() != HandlerInterfaceNamespace)
            {
                continue;
            }

            if (iface.MetadataName == ResultHandlerInterfaceMetadataName &&
                iface.TypeArguments.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], commandType))
            {
                resultMatch = iface;
            }
            else if (iface.MetadataName == VoidHandlerInterfaceMetadataName &&
                     iface.TypeArguments.Length == 1 &&
                     SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], commandType))
            {
                voidMatch = iface;
            }
        }

        if (resultMatch is not null)
        {
            resultTypeFullyQualifiedDisplayString =
                resultMatch.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return true;
        }

        return voidMatch is not null;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol implementation) =>
        implementation.InstanceConstructors.Any(static c =>
            c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);
}
