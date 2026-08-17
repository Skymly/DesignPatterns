using System;

namespace DesignPatterns.SourceGenerators.Generators;

internal readonly struct BuilderStepModel : IEquatable<BuilderStepModel>
{
    public BuilderStepModel(
        string methodName,
        string bindingName,
        string typeParameterName,
        string valueTypeDisplay,
        bool required,
        string? mutexGroup,
        string? after,
        string? before,
        LocationInfo location)
    {
        MethodName = methodName;
        BindingName = bindingName;
        TypeParameterName = typeParameterName;
        ValueTypeDisplay = valueTypeDisplay;
        Required = required;
        MutexGroup = mutexGroup;
        After = after;
        Before = before;
        Location = location;
    }

    public string MethodName { get; }
    public string BindingName { get; }
    public string TypeParameterName { get; }
    public string ValueTypeDisplay { get; }
    public bool Required { get; }
    public string? MutexGroup { get; }
    public string? After { get; }
    public string? Before { get; }
    public LocationInfo Location { get; }

    public bool Equals(BuilderStepModel other) =>
        string.Equals(MethodName, other.MethodName, StringComparison.Ordinal)
        && string.Equals(BindingName, other.BindingName, StringComparison.Ordinal)
        && string.Equals(TypeParameterName, other.TypeParameterName, StringComparison.Ordinal)
        && string.Equals(ValueTypeDisplay, other.ValueTypeDisplay, StringComparison.Ordinal)
        && Required == other.Required
        && string.Equals(MutexGroup, other.MutexGroup, StringComparison.Ordinal)
        && string.Equals(After, other.After, StringComparison.Ordinal)
        && string.Equals(Before, other.Before, StringComparison.Ordinal)
        && Location.Equals(other.Location);

    public override bool Equals(object? obj) => obj is BuilderStepModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(MethodName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(BindingName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TypeParameterName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ValueTypeDisplay ?? string.Empty);
            hash = (hash * 31) + Required.GetHashCode();
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(MutexGroup ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(After ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Before ?? string.Empty);
            hash = (hash * 31) + Location.GetHashCode();
            return hash;
        }
    }
}

internal readonly struct BuilderAssembleParameterModel : IEquatable<BuilderAssembleParameterModel>
{
    public BuilderAssembleParameterModel(
        string parameterName,
        string? boundStepMethodName,
        bool isCancellationToken = false)
    {
        ParameterName = parameterName;
        BoundStepMethodName = boundStepMethodName;
        IsCancellationToken = isCancellationToken;
    }

    public string ParameterName { get; }
    public string? BoundStepMethodName { get; }
    public bool IsCancellationToken { get; }

    public bool Equals(BuilderAssembleParameterModel other) =>
        string.Equals(ParameterName, other.ParameterName, StringComparison.Ordinal)
        && string.Equals(BoundStepMethodName, other.BoundStepMethodName, StringComparison.Ordinal)
        && IsCancellationToken == other.IsCancellationToken;

    public override bool Equals(object? obj) => obj is BuilderAssembleParameterModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(ParameterName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(BoundStepMethodName ?? string.Empty);
            return (hash * 31) + IsCancellationToken.GetHashCode();
        }
    }
}

internal sealed class GenerateBuilderModel : IEquatable<GenerateBuilderModel>
{
    public GenerateBuilderModel(
        string holderName,
        string holderFullyQualifiedName,
        string? namespaceName,
        bool assembleIsStatic,
        bool assembleIsAsync,
        string assembleMethodName,
        string productTypeDisplay,
        EquatableArray<BuilderStepModel> steps,
        EquatableArray<BuilderAssembleParameterModel> assembleParameters,
        LocationInfo location)
    {
        HolderName = holderName;
        HolderFullyQualifiedName = holderFullyQualifiedName;
        NamespaceName = namespaceName;
        AssembleIsStatic = assembleIsStatic;
        AssembleIsAsync = assembleIsAsync;
        AssembleMethodName = assembleMethodName;
        ProductTypeDisplay = productTypeDisplay;
        Steps = steps;
        AssembleParameters = assembleParameters;
        Location = location;
    }

    public string HolderName { get; }
    public string HolderFullyQualifiedName { get; }
    public string? NamespaceName { get; }
    public bool AssembleIsStatic { get; }
    public bool AssembleIsAsync { get; }
    public string AssembleMethodName { get; }
    public string ProductTypeDisplay { get; }
    public EquatableArray<BuilderStepModel> Steps { get; }
    public EquatableArray<BuilderAssembleParameterModel> AssembleParameters { get; }
    public LocationInfo Location { get; }

    public string BuilderName => HolderName + "Builder";

    public bool Equals(GenerateBuilderModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(HolderName, other.HolderName, StringComparison.Ordinal)
            && string.Equals(HolderFullyQualifiedName, other.HolderFullyQualifiedName, StringComparison.Ordinal)
            && string.Equals(NamespaceName, other.NamespaceName, StringComparison.Ordinal)
            && AssembleIsStatic == other.AssembleIsStatic
            && AssembleIsAsync == other.AssembleIsAsync
            && string.Equals(AssembleMethodName, other.AssembleMethodName, StringComparison.Ordinal)
            && string.Equals(ProductTypeDisplay, other.ProductTypeDisplay, StringComparison.Ordinal)
            && Steps.Equals(other.Steps)
            && AssembleParameters.Equals(other.AssembleParameters)
            && Location.Equals(other.Location);
    }

    public override bool Equals(object? obj) => Equals(obj as GenerateBuilderModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(HolderName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(HolderFullyQualifiedName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(NamespaceName ?? string.Empty);
            hash = (hash * 31) + AssembleIsStatic.GetHashCode();
            hash = (hash * 31) + AssembleIsAsync.GetHashCode();
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(AssembleMethodName ?? string.Empty);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ProductTypeDisplay ?? string.Empty);
            hash = (hash * 31) + Steps.GetHashCode();
            hash = (hash * 31) + AssembleParameters.GetHashCode();
            hash = (hash * 31) + Location.GetHashCode();
            return hash;
        }
    }
}
