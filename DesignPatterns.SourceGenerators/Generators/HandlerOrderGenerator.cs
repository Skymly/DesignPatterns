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

    private static readonly DiagnosticDescriptor DuplicateOrderDescriptor = new(
        id: DiagnosticIds.HandlerOrderDuplicateOrder,
        title: "Duplicate handler order",
        messageFormat: "Handler order '{0}' is already used for context '{1}'",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor HandlerContractMismatchDescriptor = new(
        id: DiagnosticIds.HandlerOrderContractMismatch,
        title: "Handler does not implement IHandler for context",
        messageFormat: "Type '{0}' does not implement IHandler<{1}>",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingParameterlessConstructorDescriptor = new(
        id: DiagnosticIds.HandlerOrderMissingParameterlessConstructor,
        title: "Handler requires a public parameterless constructor",
        messageFormat: "Type '{0}' must declare a public parameterless constructor to be used with generated handler pipelines",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            HandlerOrderMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false));

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            HandlerOrderGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true));

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()),
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static HandlerRegistration? Transform(GeneratorAttributeSyntaxContext context, bool isGenericAttribute)
    {
        if (context.TargetSymbol is not INamedTypeSymbol handlerType)
        {
            return null;
        }

        if (context.Attributes.IsDefaultOrEmpty)
        {
            return null;
        }

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

            return new HandlerRegistration(
                order,
                contextType,
                handlerType,
                context.TargetNode.GetLocation());
        }

        return null;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<HandlerRegistration?> nonGeneric,
        ImmutableArray<HandlerRegistration?> generic)
    {
        var registrations = nonGeneric
            .Concat(generic)
            .Where(static r => r is not null)
            .Cast<HandlerRegistration>()
            .ToList();

        if (registrations.Count == 0)
        {
            return;
        }

        ReportValidationDiagnostics(context, registrations);

        foreach (var group in registrations.GroupBy(static r => r.ContextType, SymbolEqualityComparer.Default))
        {
            if (group.Key is not INamedTypeSymbol contextType)
            {
                continue;
            }

            var valid = group
                .Where(r => ImplementsHandler(r.HandlerType, contextType))
                .Where(r => HasPublicParameterlessConstructor(r.HandlerType))
                .GroupBy(r => r.Order)
                .Where(g => g.Count() == 1)
                .Select(g => g.First())
                .OrderBy(r => r.Order)
                .ThenBy(r => r.HandlerType.Name, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            EmitPipeline(context, contextType, valid);
        }
    }

    private static void ReportValidationDiagnostics(
        SourceProductionContext context,
        List<HandlerRegistration> registrations)
    {
        foreach (var duplicateGroup in registrations
                     .GroupBy(static r => (r.ContextType, r.Order))
                     .Where(g => g.Count() > 1))
        {
            var contextName = duplicateGroup.First().ContextType.ToDisplayString();
            foreach (var registration in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateOrderDescriptor,
                    registration.Location,
                    registration.Order,
                    contextName));
            }
        }

        foreach (var registration in registrations.Where(r => !ImplementsHandler(r.HandlerType, r.ContextType)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HandlerContractMismatchDescriptor,
                registration.Location,
                registration.HandlerType.Name,
                registration.ContextType.ToDisplayString()));
        }

        foreach (var registration in registrations.Where(r => !HasPublicParameterlessConstructor(r.HandlerType)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingParameterlessConstructorDescriptor,
                registration.Location,
                registration.HandlerType.Name));
        }
    }

    private static void EmitPipeline(
        SourceProductionContext context,
        INamedTypeSymbol contextType,
        List<HandlerRegistration> handlers)
    {
        var namespaceName = contextType.ContainingNamespace.IsGlobalNamespace
            ? null
            : contextType.ContainingNamespace.ToDisplayString();

        var contextTypeName = contextType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var pipelineClassName = HandlerPipelineSyntaxFactory.GetPipelineClassName(contextType);
        var handlerTypeNames = handlers
            .Select(h => h.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToList();

        var compilationUnit = HandlerPipelineSyntaxFactory.CreatePipelineCompilationUnit(
            namespaceName,
            pipelineClassName,
            contextTypeName,
            handlerTypeNames);

        context.AddSource(
            $"{contextType.Name}.{pipelineClassName}.g.cs",
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

    private sealed class HandlerRegistration
    {
        public HandlerRegistration(
            int order,
            INamedTypeSymbol contextType,
            INamedTypeSymbol handlerType,
            Location location)
        {
            Order = order;
            ContextType = contextType;
            HandlerType = handlerType;
            Location = location;
        }

        public int Order { get; }

        public INamedTypeSymbol ContextType { get; }

        public INamedTypeSymbol HandlerType { get; }

        public Location Location { get; }
    }
}
