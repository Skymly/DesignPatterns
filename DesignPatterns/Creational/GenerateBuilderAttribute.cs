using System;

namespace DesignPatterns.Creational;

/// <summary>
/// Marks a builder schema holder type for compile-time generation of a typed step builder.
/// </summary>
/// <remarks>
/// Apply to a dedicated holder that declares <see cref="BuilderStepAttribute"/> methods and a
/// <see cref="BuilderAssembleAttribute"/> method. The product type is inferred from the assemble
/// method return type. This is unrelated to registration/assembly <c>*Builder</c> APIs
/// such as <see cref="FactoryRegistryBuilder"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateBuilderAttribute : Attribute
{
}
