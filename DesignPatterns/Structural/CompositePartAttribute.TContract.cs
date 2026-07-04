#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Structural;

/// <summary>
/// Marks a class as a composite part for compile-time catalog generation.
/// </summary>
/// <typeparam name="TContract">The composite contract type (interface or base class).</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CompositePartAttribute<TContract> : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositePartAttribute{TContract}"/> class.
    /// </summary>
    /// <param name="key">The unique key for this part within the composite contract.</param>
    public CompositePartAttribute(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        Key = key;
    }

    /// <summary>
    /// The unique key for this part.
    /// </summary>
    public string Key { get; }

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

#endif
