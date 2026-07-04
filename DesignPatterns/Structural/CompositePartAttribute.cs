using System;

namespace DesignPatterns.Structural;

/// <summary>
/// Marks a class as a composite part for compile-time catalog generation.
/// Use the generic attribute when the target framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CompositePartAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositePartAttribute"/> class.
    /// </summary>
    /// <param name="key">The unique key for this part within the composite contract.</param>
    /// <param name="for">The composite contract type (interface or base class).</param>
    public CompositePartAttribute(string key, Type @for)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        Key = key;
        For = @for ?? throw new ArgumentNullException(nameof(@for));
    }

    /// <summary>
    /// The unique key for this part.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The composite contract type.
    /// </summary>
    public Type For { get; }

    /// <summary>
    /// Parent part key, or <see langword="null"/> for a root candidate.
    /// </summary>
    public string? ParentKey { get; set; }

    /// <summary>
    /// Order among siblings with the same parent. Lower values come first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Allowed child implementation types, or <see langword="null"/> to allow any
    /// type implementing the contract. When set, the source generator validates
    /// that each child's implementation type is in this list.
    /// </summary>
    public Type[]? AllowedChildTypes { get; set; }
}
