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
/// Generates <c>{Holder}WorkStepKeys</c> and <c>{Holder}WorkGraph.Create</c> for
/// <c>[WorkGraph]</c> / <c>[WorkStep]</c> catalogs, reporting DP087–DP092.
/// </summary>
[Generator]
public sealed class WorkGraphGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for non-generic <c>WorkGraphAttribute</c>.</summary>
    public const string WorkGraphMetadataName = "DesignPatterns.Behavioral.WorkGraphAttribute";

    /// <summary>Metadata name for generic <c>WorkGraphAttribute&lt;TContext&gt;</c>.</summary>
    public const string WorkGraphGenericMetadataName = "DesignPatterns.Behavioral.WorkGraphAttribute`1";

    /// <summary>Metadata name for <c>WorkStepAttribute</c>.</summary>
    public const string WorkStepMetadataName = "DesignPatterns.Behavioral.WorkStepAttribute";

    private const string IWorkStepMetadataName = "DesignPatterns.Behavioral.IWorkStep`1";

    private static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included)
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var holdersNonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
                WorkGraphMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => TransformHolder(ctx, isGenericAttribute: false))
            .WithTrackingName(TrackingNames.WorkGraphHolderNonGenericTransform);

        var holdersGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
                WorkGraphGenericMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => TransformHolder(ctx, isGenericAttribute: true))
            .WithTrackingName(TrackingNames.WorkGraphHolderGenericTransform);

        var steps = context.SyntaxProvider.ForAttributeWithMetadataName(
                WorkStepMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => TransformSteps(ctx))
            .WithTrackingName(TrackingNames.WorkStepTransform);

        var holders = holdersNonGeneric.Collect()
            .Combine(holdersGeneric.Collect())
            .WithTrackingName(TrackingNames.WorkGraphHolderCombine);

        context.RegisterSourceOutput(
            holders.Combine(steps.Collect()).WithTrackingName(TrackingNames.WorkGraphCombine),
            static (spc, source) => Execute(
                spc,
                source.Left.Left,
                source.Left.Right,
                source.Right));
    }

    private static Result<WorkGraphHolderInfo> TransformHolder(
        GeneratorAttributeSyntaxContext context,
        bool isGenericAttribute)
    {
        if (context.TargetSymbol is not INamedTypeSymbol holder)
        {
            return Result<WorkGraphHolderInfo>.Empty;
        }

        if (context.Attributes.IsDefaultOrEmpty)
        {
            return Result<WorkGraphHolderInfo>.Empty;
        }

        var attribute = context.Attributes[0];
        INamedTypeSymbol? contextType = null;
        if (isGenericAttribute)
        {
            if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 })
            {
                contextType = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
            }
        }
        else if (attribute.ConstructorArguments.Length > 0)
        {
            contextType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        }

        if (contextType is null || contextType.TypeKind == TypeKind.Error)
        {
            return Result<WorkGraphHolderInfo>.Empty;
        }

        var info = new WorkGraphHolderInfo(
            holder.Name,
            holder.ContainingNamespace.IsGlobalNamespace
                ? null
                : holder.ContainingNamespace.ToDisplayString(),
            holder.ToDisplayString(FullyQualifiedFormat),
            contextType.ToDisplayString(FullyQualifiedFormat),
            contextType.Name,
            new LocationInfo(context.TargetNode.GetLocation()));

        return Result<WorkGraphHolderInfo>.Success(info);
    }

    private static EquatableArray<WorkStepInfo> TransformSteps(GeneratorAttributeSyntaxContext context)
    {
        var results = new List<WorkStepInfo>();
        if (context.TargetSymbol is not INamedTypeSymbol implementation)
        {
            return new EquatableArray<WorkStepInfo>(results.ToArray());
        }

        var compilation = context.SemanticModel.Compilation;
        var iWorkStep = compilation.GetTypeByMetadataName(IWorkStepMetadataName);

        foreach (var attribute in context.Attributes)
        {
            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var graph = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            if (graph is null || graph.TypeKind == TypeKind.Error)
            {
                continue;
            }

            string? id = null;
            var dependsOn = Array.Empty<string>();
            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == "Id" && named.Value.Value is string idValue)
                {
                    id = idValue;
                }
                else if (named.Key == "DependsOn" && !named.Value.IsNull)
                {
                    dependsOn = ExtractDependsOn(named.Value);
                }
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            // Resolve holder context type for contract check (generic or non-generic WorkGraph).
            var holderContext = TryGetHolderContextType(graph);
            var implements = holderContext is not null
                && iWorkStep is not null
                && ImplementsWorkStep(implementation, iWorkStep, holderContext);

            results.Add(new WorkStepInfo(
                id!,
                WorkGraphSyntaxFactory.ToConstantName(id!),
                implementation.ToDisplayString(FullyQualifiedFormat),
                implementation.Name,
                graph.ToDisplayString(FullyQualifiedFormat),
                new EquatableArray<string>(dependsOn),
                implements,
                new LocationInfo(context.TargetNode.GetLocation())));
        }

        return new EquatableArray<WorkStepInfo>(results.ToArray());
    }

    private static string[] ExtractDependsOn(TypedConstant value)
    {
        if (value.Kind != TypedConstantKind.Array || value.Values.IsDefaultOrEmpty)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>(value.Values.Length);
        foreach (var element in value.Values)
        {
            if (element.Value is string s && !string.IsNullOrWhiteSpace(s))
            {
                list.Add(s);
            }
        }

        return list.ToArray();
    }

    private static INamedTypeSymbol? TryGetHolderContextType(INamedTypeSymbol holder)
    {
        foreach (var attribute in holder.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (!string.Equals(attrClass.Name, "WorkGraphAttribute", StringComparison.Ordinal)
                || !string.Equals(
                    attrClass.ContainingNamespace?.ToDisplayString(),
                    "DesignPatterns.Behavioral",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (attrClass.IsGenericType && attrClass.TypeArguments.Length == 1)
            {
                return attrClass.TypeArguments[0] as INamedTypeSymbol;
            }

            if (!attrClass.IsGenericType && attribute.ConstructorArguments.Length > 0)
            {
                return attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            }
        }

        return null;
    }

    private static bool ImplementsWorkStep(
        INamedTypeSymbol implementation,
        INamedTypeSymbol iWorkStep,
        INamedTypeSymbol contextType)
    {
        foreach (var iface in implementation.AllInterfaces)
        {
            if (iface.OriginalDefinition.Equals(iWorkStep, SymbolEqualityComparer.Default)
                && iface.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], contextType))
            {
                return true;
            }
        }

        return false;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<Result<WorkGraphHolderInfo>> holdersNonGeneric,
        ImmutableArray<Result<WorkGraphHolderInfo>> holdersGeneric,
        ImmutableArray<EquatableArray<WorkStepInfo>> stepGroups)
    {
        var holders = ResultExtensions.ReportAndCollect(context, holdersNonGeneric)
            .Concat(ResultExtensions.ReportAndCollect(context, holdersGeneric))
            .GroupBy(static h => h.HolderFullyQualifiedDisplayString, StringComparer.Ordinal)
            .Select(static g => g.First())
            .ToList();

        if (holders.Count == 0)
        {
            return;
        }

        var allSteps = new List<WorkStepInfo>();
        foreach (var group in stepGroups)
        {
            foreach (var step in group)
            {
                allSteps.Add(step);
            }
        }

        var stepsByHolder = allSteps
            .GroupBy(static s => s.HolderFullyQualifiedDisplayString, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.ToList(), StringComparer.Ordinal);

        foreach (var holder in holders.OrderBy(static h => h.HolderFullyQualifiedDisplayString, StringComparer.Ordinal))
        {
            stepsByHolder.TryGetValue(holder.HolderFullyQualifiedDisplayString, out var steps);
            steps ??= new List<WorkStepInfo>();

            if (!TryBuildEmitModel(context, holder, steps, out var model))
            {
                continue;
            }

            var compilationUnit = WorkGraphSyntaxFactory.CreateCompilationUnit(model);
            var hintBase = HintNameHelper.FromString(holder.HolderFullyQualifiedDisplayString);
            context.AddSource(
                hintBase + ".WorkGraph.g.cs",
                SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8));
        }
    }

    private static bool TryBuildEmitModel(
        SourceProductionContext context,
        WorkGraphHolderInfo holder,
        List<WorkStepInfo> steps,
        out WorkGraphEmitModel model)
    {
        model = null!;
        var hasError = false;

        foreach (var step in steps)
        {
            if (!step.ImplementsContract)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.WorkGraphContractMismatch,
                    step.Location.ToLocation(),
                    step.ImplementationName,
                    holder.HolderName,
                    holder.ContextName));
                hasError = true;
            }
        }

        var byId = new Dictionary<string, WorkStepInfo>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (byId.ContainsKey(step.StepId))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.WorkGraphDuplicateStepId,
                    step.Location.ToLocation(),
                    step.StepId,
                    holder.HolderName));
                hasError = true;
                continue;
            }

            byId[step.StepId] = step;
        }

        foreach (var step in steps)
        {
            foreach (var dependency in step.DependsOn)
            {
                if (string.Equals(dependency, step.StepId, StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DesignPatternsDiagnosticDescriptors.WorkGraphSelfDependency,
                        step.Location.ToLocation(),
                        step.StepId,
                        holder.HolderName));
                    hasError = true;
                }
                else if (!byId.ContainsKey(dependency))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DesignPatternsDiagnosticDescriptors.WorkGraphUnknownDependency,
                        step.Location.ToLocation(),
                        step.StepId,
                        holder.HolderName,
                        dependency));
                    hasError = true;
                }
            }
        }

        // Cycle detection among known, non-self edges.
        var cyclicIds = FindCyclicStepIds(byId);
        if (cyclicIds.Count > 0)
        {
            var joined = string.Join(", ", cyclicIds.OrderBy(static id => id, StringComparer.Ordinal));
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.WorkGraphCycle,
                holder.Location.ToLocation(),
                holder.HolderName,
                joined));
            hasError = true;
        }

        // Unreachable: not visited from any root (empty DependsOn) via successor edges.
        var unreachable = FindUnreachableStepIds(byId);
        foreach (var id in unreachable.OrderBy(static x => x, StringComparer.Ordinal))
        {
            if (byId.TryGetValue(id, out var step))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.WorkGraphUnreachableStep,
                    step.Location.ToLocation(),
                    step.StepId,
                    holder.HolderName));
            }
        }

        // Warnings alone do not block emission; errors do.
        // Unreachable with a cycle is always accompanied by DP087 (error), so emission is blocked.
        // Pure unreachable without cycle cannot occur in a valid DAG — keep warning-only path emit-safe.
        // Empty catalogs still emit Create so Build() rejects at runtime (Spec: empty → Create Error).
        if (hasError)
        {
            return false;
        }

        var constantById = byId.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ConstantName,
            StringComparer.Ordinal);

        var emitSteps = byId.Values
            .OrderBy(static s => s.StepId, StringComparer.Ordinal)
            .Select(step =>
            {
                var depConstants = step.DependsOn
                    .Where(dep => constantById.ContainsKey(dep))
                    .Select(dep => constantById[dep])
                    .ToArray();
                return new WorkStepEmitModel(
                    step.StepId,
                    step.ConstantName,
                    new EquatableArray<string>(depConstants));
            })
            .ToArray();

        model = new WorkGraphEmitModel(
            holder.HolderName,
            holder.NamespaceName,
            holder.HolderFullyQualifiedDisplayString,
            holder.ContextFullyQualifiedDisplayString,
            new EquatableArray<WorkStepEmitModel>(emitSteps));
        return true;
    }

    private static void BuildAdjacency(
        Dictionary<string, WorkStepInfo> byId,
        out Dictionary<string, int> remainingIndeegree,
        out Dictionary<string, List<string>> successors)
    {
        remainingIndeegree = new Dictionary<string, int>(byId.Count, StringComparer.Ordinal);
        successors = new Dictionary<string, List<string>>(byId.Count, StringComparer.Ordinal);

        foreach (var id in byId.Keys)
        {
            remainingIndeegree[id] = 0;
            successors[id] = new List<string>();
        }

        foreach (var registration in byId.Values)
        {
            foreach (var dependency in registration.DependsOn)
            {
                if (string.Equals(dependency, registration.StepId, StringComparison.Ordinal)
                    || !byId.ContainsKey(dependency))
                {
                    continue;
                }

                remainingIndeegree[registration.StepId]++;
                successors[dependency].Add(registration.StepId);
            }
        }
    }

    private static HashSet<string> FindCyclicStepIds(Dictionary<string, WorkStepInfo> byId)
    {
        BuildAdjacency(byId, out var remainingIndeegree, out var successors);

        var ready = new Queue<string>();
        foreach (var pair in remainingIndeegree)
        {
            if (pair.Value == 0)
            {
                ready.Enqueue(pair.Key);
            }
        }

        var scheduled = 0;
        while (ready.Count > 0)
        {
            var id = ready.Dequeue();
            scheduled++;
            foreach (var successor in successors[id])
            {
                remainingIndeegree[successor]--;
                if (remainingIndeegree[successor] == 0)
                {
                    ready.Enqueue(successor);
                }
            }
        }

        var cyclic = new HashSet<string>(StringComparer.Ordinal);
        if (scheduled != byId.Count)
        {
            foreach (var pair in remainingIndeegree)
            {
                if (pair.Value > 0)
                {
                    cyclic.Add(pair.Key);
                }
            }
        }

        return cyclic;
    }

    private static HashSet<string> FindUnreachableStepIds(Dictionary<string, WorkStepInfo> byId)
    {
        BuildAdjacency(byId, out _, out var successors);

        // Roots: no known non-self DependsOn edges (unknown/self edges do not create predecessors).
        var roots = byId.Values
            .Where(s =>
            {
                foreach (var dep in s.DependsOn)
                {
                    if (byId.ContainsKey(dep) && !string.Equals(dep, s.StepId, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            })
            .Select(static s => s.StepId)
            .ToList();

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(roots);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id))
            {
                continue;
            }

            foreach (var successor in successors[id])
            {
                queue.Enqueue(successor);
            }
        }

        var unreachable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in byId.Keys)
        {
            if (!visited.Contains(id))
            {
                unreachable.Add(id);
            }
        }

        return unreachable;
    }
}
