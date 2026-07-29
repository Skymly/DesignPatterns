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
/// Immutable command-type-level metadata extracted from command router attributes.
/// </summary>
internal sealed record CommandInfo(
    string FullyQualifiedName,
    string Name,
    string? Namespace,
    string FullyQualifiedDisplayString);

/// <summary>
/// Immutable handler registration model collected by the incremental pipeline.
/// <see cref="ResultTypeFullyQualifiedDisplayString"/> holds <c>TResult</c> or stream
/// <c>TItem</c>; <see cref="IsStream"/> is set for <c>IStreamCommandHandler&lt;,&gt;</c>
/// (no pipeline behaviors are emitted for stream terminals).
/// </summary>
internal sealed record CommandHandlerRegistration(
    CommandInfo Command,
    string HandlerName,
    string HandlerFullyQualifiedDisplayString,
    bool ImplementsHandlerInterface,
    string? ResultTypeFullyQualifiedDisplayString,
    bool IsStream,
    bool HasPublicParameterlessConstructor,
    LocationInfo Location);

/// <summary>
/// Immutable pipeline behavior registration model collected by the incremental pipeline.
/// </summary>
internal sealed record CommandPipelineBehaviorRegistration(
    CommandInfo Command,
    int Order,
    string BehaviorName,
    string BehaviorFullyQualifiedDisplayString,
    bool ImplementsBehaviorInterface,
    string? ResultTypeFullyQualifiedDisplayString,
    bool HasPublicParameterlessConstructor,
    LocationInfo Location);

/// <summary>
/// Static behavior entry emitted into <c>RegisterAll</c> via <c>UseBehavior</c>.
/// </summary>
internal sealed record CommandPipelineBehaviorEmit(
    string BehaviorFullyQualifiedDisplayString,
    int Order,
    string? ResultTypeFullyQualifiedDisplayString);

/// <summary>
/// Generates <c>{Command}CommandHandlerRegistry</c> static classes for
/// <c>[RegisterCommandHandler]</c>-attributed handler implementations and
/// <c>[CommandPipelineBehavior]</c>-attributed pipeline behaviors.
/// Each registry exposes <c>RegisterAll(CommandRouterBuilder)</c> /
/// <c>CreateRouter()</c> for the static path and, when DI integration is enabled,
/// <c>RegisterDi</c> plus provider-based <c>RegisterAll</c>.
/// Stream handlers (<c>IStreamCommandHandler&lt;,&gt;</c>) emit arity-2 <c>Register</c>
/// bindings without pipeline behaviors.
/// </summary>
/// <remarks>
/// Ticket ownership (#259 / #264): static parameterless-ctor wiring is always emitted
/// for handlers and behaviors. DI / Autofac glue is gated by MSBuild integration flags
/// and applies to handlers only (no behavior <c>RegisterDi</c>).
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

    /// <summary>Metadata name for non-generic <c>CommandPipelineBehaviorAttribute</c>.</summary>
    public const string CommandPipelineBehaviorMetadataName = "DesignPatterns.Behavioral.CommandPipelineBehaviorAttribute";

    /// <summary>Metadata name for generic <c>CommandPipelineBehaviorAttribute&lt;TCommand&gt;</c>.</summary>
    public const string CommandPipelineBehaviorGenericMetadataName = "DesignPatterns.Behavioral.CommandPipelineBehaviorAttribute`1";

    private const string VoidHandlerInterfaceMetadataName = "ICommandHandler`1";
    private const string ResultHandlerInterfaceMetadataName = "ICommandHandler`2";
    private const string StreamHandlerInterfaceMetadataName = "IStreamCommandHandler`2";
    private const string VoidBehaviorInterfaceMetadataName = "ICommandPipelineBehavior`1";
    private const string ResultBehaviorInterfaceMetadataName = "ICommandPipelineBehavior`2";
    private const string HandlerInterfaceNamespace = "DesignPatterns.Behavioral";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var handlerNonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterCommandHandlerMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => TransformHandler(ctx, isGenericAttribute: false))
            .WithTrackingName(TrackingNames.CommandHandlerNonGenericTransform);

        var handlerGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterCommandHandlerGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => TransformHandler(ctx, isGenericAttribute: true))
            .WithTrackingName(TrackingNames.CommandHandlerGenericTransform);

        var behaviorNonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            CommandPipelineBehaviorMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => TransformBehavior(ctx, isGenericAttribute: false))
            .WithTrackingName(TrackingNames.CommandPipelineBehaviorNonGenericTransform);

        var behaviorGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            CommandPipelineBehaviorGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => TransformBehavior(ctx, isGenericAttribute: true))
            .WithTrackingName(TrackingNames.CommandPipelineBehaviorGenericTransform);

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        var handlers = handlerNonGeneric.Collect().Combine(handlerGeneric.Collect())
            .WithTrackingName(TrackingNames.CommandHandlerCombine);

        var behaviors = behaviorNonGeneric.Collect().Combine(behaviorGeneric.Collect())
            .WithTrackingName(TrackingNames.CommandPipelineBehaviorCombine);

        context.RegisterSourceOutput(
            handlers.Combine(behaviors).Combine(integrationOptions)
                .WithTrackingName(TrackingNames.CommandHandlerPipelineCombine),
            (spc, source) =>
            {
                var handlerLeft = source.Left.Left;
                var behaviorLeft = source.Left.Right;
                Execute(
                    spc,
                    handlerLeft.Left.SelectMany(static list => list).ToImmutableArray(),
                    handlerLeft.Right.SelectMany(static list => list).ToImmutableArray(),
                    behaviorLeft.Left.SelectMany(static list => list).ToImmutableArray(),
                    behaviorLeft.Right.SelectMany(static list => list).ToImmutableArray(),
                    source.Right);
            });
    }

    private static List<CommandHandlerRegistration> TransformHandler(
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

            var commandInfo = CreateCommandInfo(commandType);
            var implements = TryGetHandlerContract(
                handler,
                commandType,
                out var resultTypeDisplay,
                out var isStream);

            var location = new LocationInfo(context.TargetNode.GetLocation());
            result.Add(new CommandHandlerRegistration(
                commandInfo,
                handler.Name,
                handler.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                implements,
                resultTypeDisplay,
                isStream,
                HasPublicParameterlessConstructor(handler),
                location));
        }

        return result;
    }

    private static List<CommandPipelineBehaviorRegistration> TransformBehavior(
        GeneratorAttributeSyntaxContext context,
        bool isGenericAttribute)
    {
        var result = new List<CommandPipelineBehaviorRegistration>();

        if (context.TargetSymbol is not INamedTypeSymbol behavior)
        {
            return result;
        }

        foreach (var attribute in context.Attributes)
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not int order)
            {
                continue;
            }

            INamedTypeSymbol? commandType = null;
            if (isGenericAttribute)
            {
                if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 })
                {
                    commandType = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                }
            }
            else if (attribute.ConstructorArguments.Length > 1)
            {
                commandType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
            }

            if (commandType is null || commandType.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var commandInfo = CreateCommandInfo(commandType);
            var implements = TryGetBehaviorContract(behavior, commandType, out var resultTypeDisplay);

            var location = new LocationInfo(context.TargetNode.GetLocation());
            result.Add(new CommandPipelineBehaviorRegistration(
                commandInfo,
                order,
                behavior.Name,
                behavior.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                implements,
                resultTypeDisplay,
                HasPublicParameterlessConstructor(behavior),
                location));
        }

        return result;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<CommandHandlerRegistration> handlerNonGeneric,
        ImmutableArray<CommandHandlerRegistration> handlerGeneric,
        ImmutableArray<CommandPipelineBehaviorRegistration> behaviorNonGeneric,
        ImmutableArray<CommandPipelineBehaviorRegistration> behaviorGeneric,
        GeneratorIntegrationOptions integrationOptions)
    {
        var handlers = handlerNonGeneric.Concat(handlerGeneric).ToList();
        var behaviors = behaviorNonGeneric.Concat(behaviorGeneric).ToList();

        if (handlers.Count == 0 && behaviors.Count == 0)
        {
            return;
        }

        // Report DP074 (contract mismatch) for handlers that do not implement
        // ICommandHandler / ICommandHandler<,> / IStreamCommandHandler<,>.
        foreach (var registration in handlers.Where(static r => !r.ImplementsHandlerInterface))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.RegisterCommandHandlerContractMismatch,
                registration.Location.ToLocation(),
                registration.HandlerName,
                registration.Command.FullyQualifiedName));
        }

        // Report DP077 (behavior contract mismatch).
        foreach (var registration in behaviors.Where(static r => !r.ImplementsBehaviorInterface))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.CommandPipelineBehaviorContractMismatch,
                registration.Location.ToLocation(),
                registration.BehaviorName,
                registration.Command.FullyQualifiedName));
        }

        var commandsWithHandlerAttribute = new HashSet<string>(
            handlers.Select(static h => h.Command.FullyQualifiedName),
            StringComparer.Ordinal);

        // Report DP076 (orphan behavior — no terminal [RegisterCommandHandler]).
        foreach (var registration in behaviors.Where(r => !commandsWithHandlerAttribute.Contains(r.Command.FullyQualifiedName)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.CommandPipelineBehaviorOrphan,
                registration.Location.ToLocation(),
                registration.BehaviorName,
                registration.Command.FullyQualifiedName));
        }

        // Report DP075 (duplicate order) among contract-matching behaviors per command.
        foreach (var group in behaviors
            .Where(static r => r.ImplementsBehaviorInterface)
            .GroupBy(static r => r.Command.FullyQualifiedName, StringComparer.Ordinal))
        {
            foreach (var orderGroup in group.GroupBy(static r => r.Order).Where(static g => g.Count() > 1))
            {
                foreach (var duplicate in orderGroup.Skip(1))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DesignPatternsDiagnosticDescriptors.CommandPipelineBehaviorDuplicateOrder,
                        duplicate.Location.ToLocation(),
                        duplicate.Order,
                        duplicate.Command.FullyQualifiedName));
                }
            }
        }

        // Detect command type name collisions for HintName qualification.
        var allCommandInfos = handlers.Select(static h => h.Command)
            .Concat(behaviors.Select(static b => b.Command))
            .ToList();
        var commandNamesWithCollisions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in allCommandInfos.GroupBy(static c => c.Name, StringComparer.Ordinal))
        {
            var distinctFqns = g.Select(static c => c.FullyQualifiedName).Distinct(StringComparer.Ordinal).ToList();
            if (distinctFqns.Count > 1)
            {
                commandNamesWithCollisions.Add(g.Key);
            }
        }

        var behaviorsByCommand = behaviors
            .GroupBy(static r => r.Command.FullyQualifiedName, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.ToList(), StringComparer.Ordinal);

        // Group handlers by command type. Deduplicate same-handler dual attributes; report DP073 when
        // distinct handlers claim the same command (bijection violation).
        var byCommand = handlers
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

            // Stream handlers have no pipeline behaviors (runtime forbids UseBehavior on streams).
            var behaviorEmits = winner.IsStream
                ? (IReadOnlyList<CommandPipelineBehaviorEmit>)Array.Empty<CommandPipelineBehaviorEmit>()
                : SelectBehaviorsForEmit(
                    behaviorsByCommand.TryGetValue(winner.Command.FullyQualifiedName, out var commandBehaviors)
                        ? commandBehaviors
                        : (IReadOnlyList<CommandPipelineBehaviorRegistration>)Array.Empty<CommandPipelineBehaviorRegistration>());

            EmitRegistry(
                context,
                winner.Command,
                winner.ResultTypeFullyQualifiedDisplayString,
                staticHandlerTypeNames,
                diHandlerTypeNames,
                behaviorEmits,
                integrationOptions,
                commandNamesWithCollisions.Contains(winner.Command.Name));
        }
    }

    private static IReadOnlyList<CommandPipelineBehaviorEmit> SelectBehaviorsForEmit(
        IReadOnlyList<CommandPipelineBehaviorRegistration> commandBehaviors)
    {
        return commandBehaviors
            .Where(static r => r.ImplementsBehaviorInterface)
            .Where(static r => r.HasPublicParameterlessConstructor)
            .GroupBy(static r => r.Order)
            .Where(static g => g.Count() == 1)
            .Select(static g => g.First())
            .OrderBy(static r => r.Order)
            .ThenBy(static r => r.BehaviorFullyQualifiedDisplayString, StringComparer.Ordinal)
            .Select(static r => new CommandPipelineBehaviorEmit(
                r.BehaviorFullyQualifiedDisplayString,
                r.Order,
                r.ResultTypeFullyQualifiedDisplayString))
            .ToList();
    }

    private static void EmitRegistry(
        SourceProductionContext context,
        CommandInfo commandInfo,
        string? resultTypeFullyQualifiedDisplayString,
        IReadOnlyList<string> staticHandlerTypeNames,
        IReadOnlyList<string> diHandlerTypeNames,
        IReadOnlyList<CommandPipelineBehaviorEmit> behaviors,
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
            behaviors,
            integrationOptions);

        var hintPrefix = qualifyHintName
            ? HintNameHelper.FromString(commandInfo.FullyQualifiedDisplayString)
            : HintNameHelper.FromString(commandInfo.Name);
        context.AddSource(
            $"{hintPrefix}.{registryClassName}.g.cs",
            SourceText.From(registryUnit.ToFullString(), Encoding.UTF8));
    }

    private static CommandInfo CreateCommandInfo(INamedTypeSymbol commandType) =>
        new(
            commandType.ToDisplayString(),
            commandType.Name,
            commandType.ContainingNamespace.IsGlobalNamespace
                ? null
                : commandType.ContainingNamespace.ToDisplayString(),
            commandType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

    private static bool TryGetHandlerContract(
        INamedTypeSymbol handler,
        INamedTypeSymbol commandType,
        out string? resultTypeFullyQualifiedDisplayString,
        out bool isStream)
    {
        resultTypeFullyQualifiedDisplayString = null;
        isStream = false;
        INamedTypeSymbol? voidMatch = null;
        INamedTypeSymbol? resultMatch = null;
        INamedTypeSymbol? streamMatch = null;

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
            else if (iface.MetadataName == StreamHandlerInterfaceMetadataName &&
                     iface.TypeArguments.Length == 2 &&
                     SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], commandType))
            {
                streamMatch = iface;
            }
            else if (iface.MetadataName == VoidHandlerInterfaceMetadataName &&
                     iface.TypeArguments.Length == 1 &&
                     SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], commandType))
            {
                voidMatch = iface;
            }
        }

        // Prefer non-stream contracts when a type implements both (unusual); then stream; then void.
        if (resultMatch is not null)
        {
            resultTypeFullyQualifiedDisplayString =
                resultMatch.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return true;
        }

        if (streamMatch is not null)
        {
            resultTypeFullyQualifiedDisplayString =
                streamMatch.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            isStream = true;
            return true;
        }

        return voidMatch is not null;
    }

    private static bool TryGetBehaviorContract(
        INamedTypeSymbol behavior,
        INamedTypeSymbol commandType,
        out string? resultTypeFullyQualifiedDisplayString)
    {
        resultTypeFullyQualifiedDisplayString = null;
        INamedTypeSymbol? voidMatch = null;
        INamedTypeSymbol? resultMatch = null;

        foreach (var iface in behavior.AllInterfaces)
        {
            if (iface.ContainingNamespace.ToDisplayString() != HandlerInterfaceNamespace)
            {
                continue;
            }

            if (iface.MetadataName == ResultBehaviorInterfaceMetadataName &&
                iface.TypeArguments.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], commandType))
            {
                resultMatch = iface;
            }
            else if (iface.MetadataName == VoidBehaviorInterfaceMetadataName &&
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
