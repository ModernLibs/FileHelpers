// CA1707: Fixture identifiers use underscores for readability.
#pragma warning disable CA1707
// MA0025: Fixture bodies intentionally empty — engine populates fields via source-generated code.
#pragma warning disable MA0025
// MA0048: Multiple fixture types per file — intentional.
#pragma warning disable MA0048
// CS0649: Public fields/properties are assigned by the source-generated descriptor.
#pragma warning disable CS0649

namespace ModernLibs.FileHelpers.Tests.Parity.QuickStart.Fixtures;

// ============================================================================
// Delimited Fixtures
// ============================================================================

/// <summary>
/// Original FileHelpers QuickStart Customer record.
/// Matches the example from: https://www.filehelpers.net/quickstart/
/// </summary>
[DelimitedRecord(",")]
internal sealed class Customer
{
    [FieldOrder(1)] public string Name = string.Empty;
    [FieldOrder(2)] public int Age;
    [FieldOrder(3)] public string Country = string.Empty;
}

/// <summary>
/// Customer record with header support for read tests.
/// </summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
internal sealed class CustomerWithHeader
{
    [FieldOrder(1)] public string Name = string.Empty;
    [FieldOrder(2)] public int Age;
    [FieldOrder(3)] public string Country = string.Empty;
}

// ============================================================================
// Fixed-Length Fixtures
// ============================================================================

/// <summary>
/// Fixed-length customer record matching original FileHelpers QuickStart examples.
/// </summary>
[FixedLengthRecord]
internal sealed class FixedCustomer
{
    [FieldOrder(1)] [FieldFixedLength(20)] public string Name = string.Empty;
    [FieldOrder(2)] [FieldFixedLength(3)] public int Age;
    [FieldOrder(3)] [FieldFixedLength(10)] public string Country = string.Empty;
}

/// <summary>
/// Fixed-length record with alignment for testing write padding.
/// </summary>
[FixedLengthRecord]
internal sealed class FixedCustomerAligned
{
    [FieldOrder(1)] [FieldFixedLength(20)] [FieldAlign(AlignMode.Left)] public string Name = string.Empty;
    [FieldOrder(2)] [FieldFixedLength(3)] public int Age;
    [FieldOrder(3)] [FieldFixedLength(10)] [FieldAlign(AlignMode.Right)] public string Country = string.Empty;
}

// ============================================================================
// Streaming Fixtures
// ============================================================================

/// <summary>
/// Delimited customer for streaming tests.
/// </summary>
[DelimitedRecord(",")]
internal sealed class StreamingCustomer
{
    [FieldOrder(1)] public string Name = string.Empty;
    [FieldOrder(2)] public int Age;
    [FieldOrder(3)] public string Country = string.Empty;
}

/// <summary>
/// Delimited customer with header for streaming tests.
/// </summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
internal sealed class StreamingCustomerWithHeader
{
    [FieldOrder(1)] public string Name = string.Empty;
    [FieldOrder(2)] public int Age;
    [FieldOrder(3)] public string Country = string.Empty;
}

/// <summary>
/// Fixed-length customer for streaming tests.
/// </summary>
[FixedLengthRecord]
internal sealed class StreamingFixedCustomer
{
    [FieldOrder(1)] [FieldFixedLength(20)] public string Name = string.Empty;
    [FieldOrder(2)] [FieldFixedLength(3)] public int Age;
    [FieldOrder(3)] [FieldFixedLength(10)] public string Country = string.Empty;
}
