using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Generators.StateTransition;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates ordered handler pipelines for types marked with <c>[HandlerOrder]</c>.
/// </summary>
[Generator]
public sealed class HandlerOrderGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for non-generic <c>HandlerOrderAttribute</c>.</summary>
    public const string HandlerOrderMetadataName = "DesignPatterns.Behavioral.HandlerOrderAttribute";

    /// <summary>Metadata name for generic <c>HandlerOrderAttribute&lt;TContext&gt;</c>.</summary>
    public const string HandlerOrderGenericMetadataName = "DesignPatterns.Behavioral.HandlerOrderAttribute`1";

    private static readonly DiagnosticDescriptor DuplicateOrderDescriptor =
        DesignPatternsDiagnosticDescriptors.HandlerOrderDuplicateOrder;

    private static readonly DiagnosticDescriptor HandlerContractMismatchDescriptor =
        DesignPatternsDiagnosticDescriptors.HandlerOrderContractMismatch;

    private static readonly DiagnosticDescriptor MissingParameterlessConstructorDescriptor =
        DesignPatternsDiagnosticDescriptors.HandlerOrderMissingParameterlessConstructor;

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            HandlerOrderMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false))
            .WithTrackingName(TrackingNames.HandlerNonGenericTransform);

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            HandlerOrderGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true))
            .WithTrackingName(TrackingNames.HandlerGenericTransform);

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect())
                .WithTrackingName(TrackingNames.HandlerCombine)
                .Combine(integrationOptions)
                .WithTrackingName(TrackingNames.HandlerCombine),
            static (spc, source) => Execute(spc,
                source.Left.Left.SelectMany(static list => list).ToImmutableArray(),
                source.Left.Right.SelectMany(static list => list).ToImmutableArray(),
                source.Right));
    }

    private static EquatableArray<HandlerRegistration> Transform(GeneratorAttributeSyntaxContext context, bool isGenericAttribute)
    {
        var result = new List<HandlerRegistration>();

        if (context.TargetSymbol is not INamedTypeSymbol handlerType)
        {
            return new EquatableArray<HandlerRegistration>(result.ToArray());
        }

        if (context.Attributes.IsDefaultOrEmpty)
        {
            return new EquatableArray<HandlerRegistration>(result.ToArray());
        }

        var location = new LocationInfo(context.TargetNode.GetLocation());
        var handlerName = handlerType.Name;
        var handlerFullyQualifiedDisplayString = handlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        foreach (var attribute in context.Attributes)
        {
            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is not int order)
            {
                continue;
            }

            INamedTypeSymbol? contextType = null;
            if (isGenericAttribute)
            {
                if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 })
                {
                    contextType = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                }
            }
            else if (attribute.ConstructorArguments.Length > 1)
            {
                contextType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
            }

            if (contextType is null || contextType.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var contextInfo = new ContractInfo(
                contextType.ToDisplayString(),
                contextType.Name,
                contextType.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : contextType.ContainingNamespace.ToDisplayString(),
                contextType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            // Resolve optional Guard property from the attribute.
            string? guardName = null;
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (string.Equals(namedArg.Key, "Guard", StringComparison.Ordinal)
                    && namedArg.Value.Value is string guardValue)
                {
                    guardName = guardValue;
                    break;
                }
            }

            var guard = guardName is null
                ? default(GuardResolution)
                : GuardMethodValidator.Resolve(
                    handlerType,
                    guardName,
                    ImmutableArray.Create<ITypeSymbol>(contextType));

            result.Add(new HandlerRegistration(
                order,
                contextInfo,
                handlerName,
                handlerFullyQualifiedDisplayString,
                ImplementsHandler(handlerType, contextType),
                HasPublicParameterlessConstructor(handlerType),
                guard,
                location));
        }

        return new EquatableArray<HandlerRegistration>(result.ToArray());
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<HandlerRegistration> nonGeneric,
        ImmutableArray<HandlerRegistration> generic,
        GeneratorIntegrationOptions integrationOptions)
    {
        var registrations = nonGeneric
            .Concat(generic)
            .ToList();

        if (registrations.Count == 0)
        {
            return;
        }

        var contextNamesWithCollisions = new HashSet<string>(
            registrations
                .Select(static r => r.Context)
                .Distinct()
                .GroupBy(static c => c.Name, StringComparer.Ordinal)
                .Where(static g => g.Count() > 1)
                .Select(static g => g.Key),
            StringComparer.Ordinal);

        ReportValidationDiagnostics(context, registrations);

        foreach (var group in registrations.GroupBy(static r => r.Context.FullyQualifiedName, StringComparer.Ordinal))
        {
            var contextInfo = group.First().Context;

            var valid = group
                .Where(static r => r.ImplementsHandler)
                .Where(static r => r.HasPublicParameterlessConstructor)
                .Where(static r => r.Guard.IsValid)
                .GroupBy(static r => r.Order)
                .Where(static g => g.Count() == 1)
                .Select(static g => g.First())
                .OrderBy(static r => r.Order)
                .ThenBy(static r => r.HandlerName, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            EmitPipeline(
                context,
                contextInfo,
                valid,
                integrationOptions,
                contextNamesWithCollisions.Contains(contextInfo.Name));
        }
    }

    private static void ReportValidationDiagnostics(
        SourceProductionContext context,
        List<HandlerRegistration> registrations)
    {
        foreach (var duplicateGroup in registrations
                     .GroupBy(static r => (r.Context.FullyQualifiedName, r.Order))
                     .Where(g => g.Count() > 1))
        {
            var contextName = duplicateGroup.First().Context.FullyQualifiedName;
            foreach (var registration in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateOrderDescriptor,
                    registration.Location.ToLocation(),
                    registration.Order,
                    contextName));
            }
        }

        foreach (var registration in registrations.Where(static r => !r.ImplementsHandler))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HandlerContractMismatchDescriptor,
                registration.Location.ToLocation(),
                registration.HandlerName,
                registration.Context.FullyQualifiedName));
        }

        foreach (var registration in registrations.Where(static r => !r.HasPublicParameterlessConstructor))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingParameterlessConstructorDescriptor,
                registration.Location.ToLocation(),
                registration.HandlerName));
        }

        ReportGuardDiagnostics(context, registrations);
    }

    private static void ReportGuardDiagnostics(
        SourceProductionContext context,
        List<HandlerRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            var guard = registration.Guard;
            if (guard.IsValid)
            {
                continue;
            }

            var contextTypeDisplay = registration.Context.FullyQualifiedDisplayString;

            if (!guard.IsFound)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.HandlerOrderGuardMethodNotFound,
                    registration.Location.ToLocation(),
                    guard.Name,
                    registration.HandlerName,
                    contextTypeDisplay));
                continue;
            }

            if (!guard.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.HandlerOrderGuardMethodNotStatic,
                    registration.Location.ToLocation(),
                    guard.Name,
                    registration.HandlerName));
                continue;
            }

            if (!guard.HasValidSignature)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.HandlerOrderGuardMethodWrongSignature,
                    registration.Location.ToLocation(),
                    guard.Name,
                    registration.HandlerName,
                    contextTypeDisplay));
            }
        }
    }

    private static void EmitPipeline(
        SourceProductionContext context,
        ContractInfo contextInfo,
        List<HandlerRegistration> handlers,
        GeneratorIntegrationOptions integrationOptions,
        bool qualifyHintName)
    {
        var pipelineClassName = HandlerPipelineSyntaxFactory.GetPipelineClassName(contextInfo.Name);
        var handlerEntries = handlers
            .Select(static h => (h.HandlerFullyQualifiedDisplayString, h.Guard.MethodReference))
            .ToList();

        var compilationUnit = HandlerPipelineSyntaxFactory.CreatePipelineCompilationUnit(
            contextInfo.Namespace,
            pipelineClassName,
            contextInfo.FullyQualifiedDisplayString,
            handlerEntries,
            integrationOptions);

        var hintPrefix = qualifyHintName ? HintNameHelper.FromString(contextInfo.FullyQualifiedDisplayString) : contextInfo.Name;
        context.AddSource(
            $"{hintPrefix}.{pipelineClassName}.g.cs",
            SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8));
    }

    private static bool ImplementsHandler(INamedTypeSymbol handlerType, INamedTypeSymbol contextType)
    {
        foreach (var iface in handlerType.AllInterfaces)
        {
            if (iface.Name != "IHandler" || iface.TypeArguments.Length != 1)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], contextType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol handlerType) =>
        handlerType.InstanceConstructors.Any(static c =>
            c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);

    private sealed record HandlerRegistration(
        int Order,
        ContractInfo Context,
        string HandlerName,
        string HandlerFullyQualifiedDisplayString,
        bool ImplementsHandler,
        bool HasPublicParameterlessConstructor,
        GuardResolution Guard,
        LocationInfo Location);
}
