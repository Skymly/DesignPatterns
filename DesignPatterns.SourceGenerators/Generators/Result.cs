using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// A value-equatable diagnostic descriptor carrier. Stores everything needed
/// to reconstruct a <see cref="Diagnostic"/> in the source output stage,
/// while participating in incremental pipeline caching via value equality on
/// <see cref="Descriptor"/> id and <see cref="Location"/> identity.
/// </summary>
internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    /// <summary>Descriptor used to create the <see cref="Diagnostic"/>.</summary>
    public DiagnosticDescriptor Descriptor { get; }

    /// <summary>Location where the diagnostic should be reported (may be null).</summary>
    public Location? Location { get; }

    /// <summary>Message arguments passed to <c>Diagnostic.Create</c>.</summary>
    public object?[] MessageArgs { get; }

    /// <summary>
    /// Creates a <see cref="DiagnosticInfo"/> from a descriptor, optional
    /// location, and message arguments.
    /// </summary>
    public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, params object?[] messageArgs)
    {
        Descriptor = descriptor;
        Location = location;
        MessageArgs = messageArgs ?? Array.Empty<object?>();
    }

    /// <summary>Reconstructs the <see cref="Diagnostic"/> for reporting.</summary>
    public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location, MessageArgs)!;

    /// <inheritdoc />
    public bool Equals(DiagnosticInfo other) =>
        string.Equals(Descriptor.Id, other.Descriptor.Id, StringComparison.Ordinal)
        && ReferenceEquals(Location, other.Location);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DiagnosticInfo other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Descriptor.Id?.GetHashCode() ?? 0;

    public static bool operator ==(DiagnosticInfo left, DiagnosticInfo right) => left.Equals(right);

    public static bool operator !=(DiagnosticInfo left, DiagnosticInfo right) => !left.Equals(right);
}

/// <summary>
/// Wraps a transform result together with diagnostics collected during
/// extraction. Used by incremental generator <c>Transform</c> methods so
/// that per-target diagnostics flow through the pipeline alongside the
/// extracted model, rather than being silently dropped when the transform
/// returns <c>null</c>.
/// </summary>
/// <typeparam name="T">The model type — must be value-equatable for caching.</typeparam>
internal readonly struct Result<T> : IEquatable<Result<T>>
    where T : IEquatable<T>
{
    /// <summary>The extracted model, or <c>null</c> when validation failed.</summary>
    public T? Value { get; }

    /// <summary>Diagnostics collected during extraction.</summary>
    public EquatableArray<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// Creates a <see cref="Result{T}"/> with the given value and diagnostics.
    /// Prefer <c>Success</c> or the <c>Failure</c> overloads in normal code.
    /// </summary>
    public Result(T? value, EquatableArray<DiagnosticInfo> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    /// <summary><c>true</c> when <see cref="Value"/> is non-null.</summary>
    public bool HasValue => Value is not null;

    /// <summary>
    /// Creates an empty result — no value, no diagnostics. Used for silent
    /// skips where the transform cannot produce a model but no user-facing
    /// diagnostic is appropriate (e.g. error-type symbols, defensive checks
    /// that cannot occur given the syntax predicate).
    /// </summary>
    public static Result<T> Empty => new(default, default);

    /// <summary>Creates a successful result with no diagnostics.</summary>
    public static Result<T> Success(T value) => new(value, default);

    /// <summary>Creates a failed result carrying a single diagnostic.</summary>
    public static Result<T> Failure(DiagnosticInfo diagnostic) =>
        new(default, new EquatableArray<DiagnosticInfo>(new[] { diagnostic }));

    /// <summary>Creates a failed result carrying multiple diagnostics.</summary>
    public static Result<T> Failure(EquatableArray<DiagnosticInfo> diagnostics) =>
        new(default, diagnostics);

    /// <summary>Creates a failed result carrying multiple diagnostics from a list.</summary>
    public static Result<T> Failure(List<DiagnosticInfo> diagnostics) =>
        new(default, new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));

    /// <inheritdoc />
    public bool Equals(Result<T> other)
    {
        if (Value is null && other.Value is null)
            return Diagnostics.Equals(other.Diagnostics);
        if (Value is null || other.Value is null)
            return false;
        return Value.Equals(other.Value) && Diagnostics.Equals(other.Diagnostics);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = Value?.GetHashCode() ?? 0;
        hash = (hash * 31) + Diagnostics.GetHashCode();
        return hash;
    }

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
}

/// <summary>
/// Convenience helpers for working with <see cref="Result{T}"/> in
/// source output callbacks.
/// </summary>
internal static class ResultExtensions
{
    /// <summary>
    /// Reports all diagnostics from <paramref name="result"/> to
    /// <paramref name="context"/>. Returns <c>true</c> when the result
    /// carries a value (caller may proceed to emit generated source).
    /// </summary>
    internal static bool TryReportAndUnwrap<T>(
        SourceProductionContext context,
        Result<T> result,
        out T value)
        where T : IEquatable<T>
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        if (result.Value is not null)
        {
            value = result.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Reports all diagnostics from a collection of <see cref="Result{T}"/>
    /// to <paramref name="context"/>, then returns all non-null values.
    /// </summary>
    internal static List<T> ReportAndCollect<T>(
        SourceProductionContext context,
        System.Collections.Generic.IEnumerable<Result<T>> results)
        where T : IEquatable<T>
    {
        var values = new List<T>();
        foreach (var result in results)
        {
            if (TryReportAndUnwrap(context, result, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }
}
