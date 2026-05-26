using System;

namespace DesignPatterns.Creational;

/// <summary>
/// Marks a <c>partial</c> class for compile-time singleton generation using <see cref="Lazy{T}"/>.
/// Not related to dependency-injection service lifetimes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateSingletonAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/> (default), uses <see cref="System.Threading.LazyThreadSafetyMode.ExecutionAndPublication"/>.
    /// When <see langword="false"/>, uses <see cref="System.Threading.LazyThreadSafetyMode.None"/>.
    /// </summary>
    public bool ThreadSafe { get; set; } = true;
}
