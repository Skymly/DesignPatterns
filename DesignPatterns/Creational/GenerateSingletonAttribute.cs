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

    /// <summary>
    /// Gets or sets the name of a static asynchronous initialization method.
    /// When specified, the generator emits <c>GetInstanceAsync()</c> instead
    /// of <c>Instance</c>. The method must accept the generated instance and a
    /// <see cref="System.Threading.CancellationToken"/>, and return
    /// <see cref="System.Threading.Tasks.Task"/> or
    /// <see cref="System.Threading.Tasks.ValueTask"/>.
    /// </summary>
    public string? InitializeAsync { get; set; }
}
