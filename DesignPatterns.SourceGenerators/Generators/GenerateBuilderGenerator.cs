using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates typed step builders for types marked with <c>[GenerateBuilder]</c>.
/// </summary>
[Generator]
public sealed class GenerateBuilderGenerator : IIncrementalGenerator
{
    /// <summary>Full metadata name of <c>GenerateBuilderAttribute</c>.</summary>
    public const string AttributeMetadataName = "DesignPatterns.Creational.GenerateBuilderAttribute";

    private const string BuilderStepAttributeMetadataName = "DesignPatterns.Creational.BuilderStepAttribute";
    private const string BuilderAssembleAttributeMetadataName = "DesignPatterns.Creational.BuilderAssembleAttribute";
    private const int RequiredStepCap = 8;

    private static readonly SymbolDisplayFormat TypeDisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included)
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AttributeMetadataName,
                    static (node, _) => node is TypeDeclarationSyntax,
                    static (ctx, _) => Transform(ctx))
                .WithTrackingName(TrackingNames.GenerateBuilderTransform),
            static (spc, result) => Execute(spc, result));
    }

    private static Result<GenerateBuilderModel> Transform(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol holder)
        {
            return Result<GenerateBuilderModel>.Empty;
        }

        var location = new LocationInfo(context.TargetNode.GetLocation());
        var diagnostics = new List<DiagnosticInfo>();

        if (holder.TypeKind != TypeKind.Class
            || holder.IsGenericType
            || holder.IsUnboundGenericType
            || !IsAccessibleHolder(holder))
        {
            diagnostics.Add(new DiagnosticInfo(
                DesignPatternsDiagnosticDescriptors.GenerateBuilderInvalidHolder,
                location,
                holder.Name));
            return Result<GenerateBuilderModel>.Failure(diagnostics);
        }

        var stepMethods = new List<IMethodSymbol>();
        var assembleMethods = new List<IMethodSymbol>();

        foreach (var member in holder.GetMembers())
        {
            if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
            {
                continue;
            }

            if (HasAttribute(method, BuilderStepAttributeMetadataName))
            {
                stepMethods.Add(method);
            }

            if (HasAttribute(method, BuilderAssembleAttributeMetadataName))
            {
                assembleMethods.Add(method);
            }
        }

        if (assembleMethods.Count == 0)
        {
            diagnostics.Add(new DiagnosticInfo(
                DesignPatternsDiagnosticDescriptors.GenerateBuilderMissingAssemble,
                location,
                holder.Name));
            return Result<GenerateBuilderModel>.Failure(diagnostics);
        }

        if (assembleMethods.Count > 1
            || assembleMethods[0].ReturnsVoid
            || assembleMethods[0].ReturnType.SpecialType == SpecialType.System_Void)
        {
            var assemble = assembleMethods[0];
            diagnostics.Add(new DiagnosticInfo(
                DesignPatternsDiagnosticDescriptors.GenerateBuilderAssembleContractMismatch,
                new LocationInfo(assemble.Locations.FirstOrDefault()),
                assemble.Name,
                holder.Name));
            return Result<GenerateBuilderModel>.Failure(diagnostics);
        }

        var assembleMethod = assembleMethods[0];
        if (!assembleMethod.IsStatic)
        {
            // Generated code lives in the consumer assembly, so the ctor must be accessible.
            var hasAccessibleParameterlessCtor = holder.InstanceConstructors.Any(static c =>
                !c.IsStatic
                && c.Parameters.Length == 0
                && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);
            if (!hasAccessibleParameterlessCtor
                || assembleMethod.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected)
            {
                diagnostics.Add(new DiagnosticInfo(
                    DesignPatternsDiagnosticDescriptors.GenerateBuilderAssembleContractMismatch,
                    new LocationInfo(assembleMethod.Locations.FirstOrDefault()),
                    assembleMethod.Name,
                    holder.Name));
                return Result<GenerateBuilderModel>.Failure(diagnostics);
            }
        }

        var steps = new List<BuilderStepModel>();
        var bindingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var methodNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in stepMethods)
        {
            var stepLocation = new LocationInfo(method.Locations.FirstOrDefault());
            var meta = ReadStepMetadata(method);
            var bindingName = GetBindingName(method.Name);

            if (!methodNames.Add(method.Name) || !bindingNames.Add(bindingName))
            {
                diagnostics.Add(new DiagnosticInfo(
                    DesignPatternsDiagnosticDescriptors.GenerateBuilderDuplicateStep,
                    stepLocation,
                    method.Name,
                    holder.Name));
                continue;
            }

            var valueType = GetStepValueTypeDisplay(method);
            var typeParameterName = "T" + ToPascal(bindingName);
            steps.Add(new BuilderStepModel(
                method.Name,
                bindingName,
                typeParameterName,
                valueType,
                meta.Required,
                meta.MutexGroup,
                meta.After,
                meta.Before,
                stepLocation));
        }

        if (diagnostics.Count > 0 && steps.Count == 0)
        {
            return Result<GenerateBuilderModel>.Failure(diagnostics);
        }

        var requiredCount = steps.Count(static s => s.Required);
        if (requiredCount > RequiredStepCap)
        {
            diagnostics.Add(new DiagnosticInfo(
                DesignPatternsDiagnosticDescriptors.GenerateBuilderRequiredStepCapExceeded,
                location,
                holder.Name,
                requiredCount));
        }

        ValidateMutexGroups(holder.Name, steps, diagnostics);
        ValidatePartialOrder(holder.Name, steps, diagnostics);

        var assembleParameters = new List<BuilderAssembleParameterModel>();
        foreach (var parameter in assembleMethod.Parameters)
        {
            var match = FindStepForParameter(parameter.Name, steps);
            if (match is null)
            {
                diagnostics.Add(new DiagnosticInfo(
                    DesignPatternsDiagnosticDescriptors.GenerateBuilderAssembleParameterMismatch,
                    new LocationInfo(parameter.Locations.FirstOrDefault() ?? assembleMethod.Locations.FirstOrDefault()),
                    parameter.Name,
                    holder.Name));
                continue;
            }

            assembleParameters.Add(new BuilderAssembleParameterModel(parameter.Name, match.Value.MethodName));
        }

        if (diagnostics.Count > 0)
        {
            return Result<GenerateBuilderModel>.Failure(diagnostics);
        }

        var namespaceName = holder.ContainingNamespace.IsGlobalNamespace
            ? null
            : holder.ContainingNamespace.ToDisplayString();

        var model = new GenerateBuilderModel(
            holder.Name,
            holder.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            namespaceName,
            assembleMethod.IsStatic,
            assembleMethod.Name,
            assembleMethod.ReturnType.ToDisplayString(TypeDisplayFormat),
            new EquatableArray<BuilderStepModel>(steps.ToArray()),
            new EquatableArray<BuilderAssembleParameterModel>(assembleParameters.ToArray()),
            location);

        return Result<GenerateBuilderModel>.Success(model);
    }

    private static void Execute(SourceProductionContext context, Result<GenerateBuilderModel> result)
    {
        if (!ResultExtensions.TryReportAndUnwrap(context, result, out var model))
        {
            return;
        }

        var compilationUnit = GenerateBuilderSyntaxFactory.CreateCompilationUnit(model);
        var hintName = HintNameHelper.FromString(model.HolderFullyQualifiedName) + ".Builder.g.cs";
        context.AddSource(hintName, SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8));
    }

    /// <summary>
    /// Generated builders are top-level peers; private/protected holders (including nested)
    /// cannot be referenced from the emitted <c>Build</c> call.
    /// </summary>
    private static bool IsAccessibleHolder(INamedTypeSymbol holder)
    {
        for (INamedTypeSymbol? current = holder; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool HasAttribute(IMethodSymbol method, string metadataName) =>
        method.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeClass
            && string.Equals(attributeClass.ToDisplayString(), metadataName, StringComparison.Ordinal));

    private static (bool Required, string? MutexGroup, string? After, string? Before) ReadStepMetadata(
        IMethodSymbol method)
    {
        var required = true;
        string? mutexGroup = null;
        string? after = null;
        string? before = null;

        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is null
                || !string.Equals(
                    attribute.AttributeClass.ToDisplayString(),
                    BuilderStepAttributeMetadataName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var named in attribute.NamedArguments)
            {
                switch (named.Key)
                {
                    case "Required" when named.Value.Value is bool requiredValue:
                        required = requiredValue;
                        break;
                    case "MutexGroup" when named.Value.Value is string mutex:
                        mutexGroup = string.IsNullOrWhiteSpace(mutex) ? null : mutex;
                        break;
                    case "After" when named.Value.Value is string afterValue:
                        after = string.IsNullOrWhiteSpace(afterValue) ? null : afterValue;
                        break;
                    case "Before" when named.Value.Value is string beforeValue:
                        before = string.IsNullOrWhiteSpace(beforeValue) ? null : beforeValue;
                        break;
                }
            }
        }

        return (required, mutexGroup, after, before);
    }

    private static string GetStepValueTypeDisplay(IMethodSymbol method)
    {
        if (method.Parameters.Length == 0)
        {
            return "bool";
        }

        if (method.Parameters.Length == 1)
        {
            return method.Parameters[0].Type.ToDisplayString(TypeDisplayFormat);
        }

        var parts = method.Parameters.Select(static p => p.Type.ToDisplayString(TypeDisplayFormat));
        return "(" + string.Join(", ", parts) + ")";
    }

    private static string GetBindingName(string methodName)
    {
        if (methodName.Length > 4
            && methodName.StartsWith("With", StringComparison.Ordinal)
            && char.IsUpper(methodName[4]))
        {
            return methodName.Substring(4);
        }

        return methodName;
    }

    private static string ToPascal(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Step";
        }

        var builder = new StringBuilder(name.Length);
        if (!char.IsLetter(name[0]) && name[0] != '_')
        {
            builder.Append('T');
        }

        builder.Append(char.ToUpperInvariant(name[0]));
        for (var i = 1; i < name.Length; i++)
        {
            builder.Append(char.IsLetterOrDigit(name[i]) ? name[i] : '_');
        }

        return builder.ToString();
    }

    private static BuilderStepModel? FindStepForParameter(string parameterName, List<BuilderStepModel> steps)
    {
        foreach (var step in steps)
        {
            if (string.Equals(parameterName, step.BindingName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parameterName, step.MethodName, StringComparison.OrdinalIgnoreCase))
            {
                return step;
            }
        }

        return null;
    }

    private static void ValidateMutexGroups(
        string holderName,
        List<BuilderStepModel> steps,
        List<DiagnosticInfo> diagnostics)
    {
        var groups = steps
            .Where(static s => !string.IsNullOrEmpty(s.MutexGroup))
            .GroupBy(static s => s.MutexGroup!, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var requiredInGroup = group.Where(static s => s.Required).ToList();
            if (requiredInGroup.Count < 2)
            {
                continue;
            }

            for (var i = 1; i < requiredInGroup.Count; i++)
            {
                diagnostics.Add(new DiagnosticInfo(
                    DesignPatternsDiagnosticDescriptors.GenerateBuilderMutexConflict,
                    requiredInGroup[i].Location,
                    requiredInGroup[0].MethodName,
                    requiredInGroup[i].MethodName,
                    group.Key,
                    holderName));
            }
        }
    }

    private static void ValidatePartialOrder(
        string holderName,
        List<BuilderStepModel> steps,
        List<DiagnosticInfo> diagnostics)
    {
        var edges = new List<(string From, string To, BuilderStepModel Source)>();

        foreach (var step in steps)
        {
            if (!string.IsNullOrEmpty(step.After))
            {
                if (!TryResolveStepReference(step.After!, steps, out var afterName))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DesignPatternsDiagnosticDescriptors.GenerateBuilderUnknownStepReference,
                        step.Location,
                        step.MethodName,
                        holderName,
                        step.After));
                }
                else
                {
                    // After X means X must come before this step: edge X -> step
                    edges.Add((afterName!, step.MethodName, step));
                }
            }

            if (!string.IsNullOrEmpty(step.Before))
            {
                if (!TryResolveStepReference(step.Before!, steps, out var beforeName))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DesignPatternsDiagnosticDescriptors.GenerateBuilderUnknownStepReference,
                        step.Location,
                        step.MethodName,
                        holderName,
                        step.Before));
                }
                else
                {
                    // Before X means this step must come before X: edge step -> X
                    edges.Add((step.MethodName, beforeName!, step));
                }
            }
        }

        if (HasCycle(edges.Select(static e => (e.From, e.To)).ToList(), out var cycleFrom, out var cycleTo))
        {
            var source = edges.First(e =>
                string.Equals(e.From, cycleFrom, StringComparison.Ordinal)
                && string.Equals(e.To, cycleTo, StringComparison.Ordinal)).Source;
            diagnostics.Add(new DiagnosticInfo(
                DesignPatternsDiagnosticDescriptors.GenerateBuilderPartialOrderViolation,
                source.Location,
                source.MethodName,
                holderName,
                string.Equals(source.MethodName, cycleFrom, StringComparison.Ordinal) ? cycleTo : cycleFrom));
        }
    }

    private static bool TryResolveStepReference(
        string reference,
        List<BuilderStepModel> steps,
        out string? methodName)
    {
        foreach (var step in steps)
        {
            if (string.Equals(reference, step.MethodName, StringComparison.Ordinal)
                || string.Equals(reference, step.BindingName, StringComparison.OrdinalIgnoreCase))
            {
                methodName = step.MethodName;
                return true;
            }
        }

        methodName = null;
        return false;
    }

    private static bool HasCycle(
        List<(string From, string To)> edges,
        out string? cycleFrom,
        out string? cycleTo)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (from, to) in edges)
        {
            if (!adjacency.TryGetValue(from, out var list))
            {
                list = new List<string>();
                adjacency[from] = list;
            }

            list.Add(to);
        }

        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        cycleFrom = null;
        cycleTo = null;

        foreach (var node in adjacency.Keys.ToList())
        {
            if (Dfs(node, adjacency, state, ref cycleFrom, ref cycleTo))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Dfs(
        string node,
        Dictionary<string, List<string>> adjacency,
        Dictionary<string, int> state,
        ref string? cycleFrom,
        ref string? cycleTo)
    {
        state[node] = 1;
        if (adjacency.TryGetValue(node, out var neighbors))
        {
            foreach (var next in neighbors)
            {
                if (!state.TryGetValue(next, out var nextState))
                {
                    if (Dfs(next, adjacency, state, ref cycleFrom, ref cycleTo))
                    {
                        return true;
                    }
                }
                else if (nextState == 1)
                {
                    cycleFrom = node;
                    cycleTo = next;
                    return true;
                }
            }
        }

        state[node] = 2;
        return false;
    }
}
