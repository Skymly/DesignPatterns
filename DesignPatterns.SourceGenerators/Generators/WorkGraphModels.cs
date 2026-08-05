using System;
using System.Collections.Generic;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Holder type annotated with <c>[WorkGraph]</c> / <c>[WorkGraph&lt;TContext&gt;]</c>.
/// </summary>
internal sealed record WorkGraphHolderInfo(
    string HolderName,
    string? NamespaceName,
    string HolderFullyQualifiedDisplayString,
    string ContextFullyQualifiedDisplayString,
    string ContextName,
    LocationInfo Location);

/// <summary>
/// One <c>[WorkStep]</c> registration bound to a holder.
/// </summary>
internal sealed record WorkStepInfo(
    string StepId,
    string ConstantName,
    string ImplementationFullyQualifiedDisplayString,
    string ImplementationName,
    string HolderFullyQualifiedDisplayString,
    EquatableArray<string> DependsOn,
    bool ImplementsContract,
    LocationInfo Location) : IEquatable<WorkStepInfo>
{
    public bool Equals(WorkStepInfo? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(StepId, other.StepId, StringComparison.Ordinal)
            && string.Equals(ConstantName, other.ConstantName, StringComparison.Ordinal)
            && string.Equals(ImplementationFullyQualifiedDisplayString, other.ImplementationFullyQualifiedDisplayString, StringComparison.Ordinal)
            && string.Equals(ImplementationName, other.ImplementationName, StringComparison.Ordinal)
            && string.Equals(HolderFullyQualifiedDisplayString, other.HolderFullyQualifiedDisplayString, StringComparison.Ordinal)
            && DependsOn.Equals(other.DependsOn)
            && ImplementsContract == other.ImplementsContract
            && Location.Equals(other.Location);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(StepId ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ConstantName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ImplementationFullyQualifiedDisplayString ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ImplementationName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(HolderFullyQualifiedDisplayString ?? string.Empty);
            hash = (hash * 31) + DependsOn.GetHashCode();
            hash = (hash * 31) + ImplementsContract.GetHashCode();
            hash = (hash * 31) + Location.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Validated catalog ready for Keys + Create emission.
/// </summary>
internal sealed record WorkGraphEmitModel(
    string HolderName,
    string? NamespaceName,
    string HolderFullyQualifiedDisplayString,
    string ContextFullyQualifiedDisplayString,
    EquatableArray<WorkStepEmitModel> Steps) : IEquatable<WorkGraphEmitModel>
{
    public bool Equals(WorkGraphEmitModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(HolderName, other.HolderName, StringComparison.Ordinal)
            && string.Equals(NamespaceName, other.NamespaceName, StringComparison.Ordinal)
            && string.Equals(HolderFullyQualifiedDisplayString, other.HolderFullyQualifiedDisplayString, StringComparison.Ordinal)
            && string.Equals(ContextFullyQualifiedDisplayString, other.ContextFullyQualifiedDisplayString, StringComparison.Ordinal)
            && Steps.Equals(other.Steps);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(HolderName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(NamespaceName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(HolderFullyQualifiedDisplayString ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ContextFullyQualifiedDisplayString ?? string.Empty);
            hash = (hash * 31) + Steps.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// One step row in an emit model.
/// </summary>
internal sealed record WorkStepEmitModel(
    string StepId,
    string ConstantName,
    EquatableArray<string> DependsOnConstantNames) : IEquatable<WorkStepEmitModel>
{
    public bool Equals(WorkStepEmitModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(StepId, other.StepId, StringComparison.Ordinal)
            && string.Equals(ConstantName, other.ConstantName, StringComparison.Ordinal)
            && DependsOnConstantNames.Equals(other.DependsOnConstantNames);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(StepId ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ConstantName ?? string.Empty);
            hash = (hash * 31) + DependsOnConstantNames.GetHashCode();
            return hash;
        }
    }
}
