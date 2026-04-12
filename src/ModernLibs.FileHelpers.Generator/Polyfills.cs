// Polyfill for netstandard2.0: C# 9 record types and init setters require IsExternalInit.
// This type is defined in System.Runtime.dll for .NET 5+ but is missing from netstandard2.0.
// The compiler looks for it by fully-qualified name in any assembly in the compilation.
// MA0048: File name intentionally does not match type name — polyfill file collects multiple polyfills.
#pragma warning disable MA0048
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
