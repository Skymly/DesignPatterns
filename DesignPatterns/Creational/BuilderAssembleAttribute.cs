using System;

namespace DesignPatterns.Creational;

/// <summary>
/// Marks the user-authored assemble method on a <see cref="GenerateBuilderAttribute"/> schema holder.
/// </summary>
/// <remarks>
/// The assemble method receives step values (bound by parameter name to steps) and returns the
/// product. The generated <c>{Holder}Builder.Build()</c> invokes this method; the generator does
/// not invent object-mapping. The method may also be called directly when bypassing the generator.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class BuilderAssembleAttribute : Attribute
{
}
