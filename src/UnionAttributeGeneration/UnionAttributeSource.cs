﻿namespace Dunet.UnionAttributeGeneration;

internal static class UnionAttributeSource
{
    public const string Namespace = "Dunet";
    public const string Name = "UnionAttribute";
    public const string FullyQualifiedName = $"{Namespace}.{Name}";

    public const string SourceCode = """
using System;

namespace Dunet;

/// <summary>
/// Enables dunet union source generation for the decorated partial record.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class UnionAttribute : Attribute {}
""";
}
