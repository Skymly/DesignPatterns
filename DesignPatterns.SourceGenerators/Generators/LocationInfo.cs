using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// A value-equatable representation of a <see cref="Location"/>. Used in
/// incremental generator models to ensure correct caching by the Roslyn
/// incremental pipeline — <see cref="Location"/> uses reference equality,
/// which breaks model value equality when <see cref="Compilation.Clone"/>
/// creates new <see cref="Location"/> objects.
/// </summary>
internal readonly struct LocationInfo : IEquatable<LocationInfo>
{
    public string? FilePath { get; }
    public TextSpan TextSpan { get; }
    public LinePositionSpan LineSpan { get; }

    public LocationInfo(Location? location)
    {
        if (location is null)
        {
            FilePath = null;
            TextSpan = default;
            LineSpan = default;
            return;
        }

        FilePath = location.SourceTree?.FilePath ?? location.GetLineSpan().Path;
        TextSpan = location.SourceSpan;
        LineSpan = location.GetLineSpan().Span;
    }

    /// <summary>
    /// Reconstructs a <see cref="Location"/> suitable for diagnostic reporting.
    /// The returned location is an <see cref="ExternalFileLocation"/> that does
    /// not reference a <see cref="SyntaxTree"/>, which is sufficient for
    /// <see cref="Diagnostic.Create(DiagnosticDescriptor, Location, object[])"/>.
    /// </summary>
    public Location? ToLocation()
    {
        if (FilePath is null)
        {
            return null;
        }

        return Location.Create(FilePath, TextSpan, LineSpan);
    }

    public bool Equals(LocationInfo other)
    {
        return string.Equals(FilePath, other.FilePath, StringComparison.Ordinal)
            && TextSpan == other.TextSpan
            && LineSpan == other.LineSpan;
    }

    public override bool Equals(object? obj) => obj is LocationInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((FilePath?.GetHashCode() ?? 0) * 397) ^ TextSpan.GetHashCode() ^ LineSpan.GetHashCode();
        }
    }

    public static bool operator ==(LocationInfo left, LocationInfo right) => left.Equals(right);

    public static bool operator !=(LocationInfo left, LocationInfo right) => !left.Equals(right);
}
