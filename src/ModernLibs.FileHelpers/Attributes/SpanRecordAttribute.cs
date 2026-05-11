namespace ModernLibs.FileHelpers;

/// <summary>
/// Marks a <c>ref struct</c> record type for source-generated parsing support.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a <c>ref struct</c> type annotated with <see cref="DelimitedRecordAttribute"/>
/// or <see cref="FixedLengthRecordAttribute"/>, the source generator emits an
/// <c>ISpanRecordDescriptor&lt;T&gt;</c> implementation instead of the standard
/// <see cref="IRecordDescriptor{T}"/>. This enables zero-allocation parsing directly
/// into the ref struct without any heap allocations.
/// </para>
/// <para>
/// <strong>Constraints:</strong> The type must be a <c>ref struct</c> with only
/// <c>ReadOnlySpan&lt;char&gt;</c> or primitive fields. The generated descriptor
/// cannot be used with <see cref="FileHelperEngine{T}"/> batch methods (which require
/// arrays), but works with callback-based streaming APIs.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class SpanRecordAttribute : Attribute
{
}
