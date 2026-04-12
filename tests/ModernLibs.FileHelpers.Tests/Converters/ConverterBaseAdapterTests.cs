// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>
/// Tests for CONV-10 (ConverterBase bridge adapter).
/// </summary>
[Category("Phase06")]
[Category("Converters")]
public sealed class ConverterBaseAdapterTests
{
    // Helper: a ConverterBase subclass that returns a fixed object from StringToField
    // and delegates FieldToString to field.ToString().
    private sealed class FixedReturnConverter : ConverterBase
    {
        private readonly object? _returnValue;

        public FixedReturnConverter(object? returnValue) => _returnValue = returnValue;

        public override object? StringToField(string text) => _returnValue;

        public override string? FieldToString(object? field) => field?.ToString();
    }

    // Helper: a ConverterBase subclass that returns null from FieldToString.
    private sealed class NullFieldToStringConverter : ConverterBase
    {
        public override object? StringToField(string text) => text;

        public override string? FieldToString(object? field) => null;
    }

    [Test]
    public async Task Adapter_TryParse_DelegatesToStringToField()
    {
        // FixedReturnConverter returns the int 42 regardless of input
        var inner = new FixedReturnConverter(42);
        var adapter = new ConverterBaseAdapter<int>(inner);

        bool ok = adapter.TryParse("anything", out int value);

        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Adapter_TryFormat_DelegatesToFieldToString()
    {
        // FixedReturnConverter.FieldToString delegates to field.ToString(), so 99.ToString() = "99"
        var inner = new FixedReturnConverter(null);
        var adapter = new ConverterBaseAdapter<int>(inner);

        char[] buf = new char[10];
        bool ok = adapter.TryFormat(99, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(ok).IsTrue();
        await Assert.That(formatted).IsEqualTo("99");
    }

    [Test]
    public async Task Adapter_TryParse_InvalidCast_ReturnsFalse()
    {
        // StringToField returns a string "42" but adapter expects int — cast fails
        var inner = new FixedReturnConverter("42");
        var adapter = new ConverterBaseAdapter<int>(inner);

        bool ok = adapter.TryParse("42", out int value);

        await Assert.That(ok).IsFalse();
        await Assert.That(value).IsEqualTo(default(int));
    }

    [Test]
    public async Task Adapter_TryFormat_NullResult_WritesEmpty()
    {
        var inner = new NullFieldToStringConverter();
        var adapter = new ConverterBaseAdapter<string>(inner);

        char[] buf = new char[10];
        bool ok = adapter.TryFormat("anything", buf, out int cw);

        await Assert.That(ok).IsTrue();
        await Assert.That(cw).IsEqualTo(0);
    }

    [Test]
    public async Task ConverterBase_IsAbstractClass_WithStringToFieldAndFieldToString()
    {
        await Assert.That(typeof(ConverterBase).IsAbstract).IsTrue();
        await Assert.That(typeof(ConverterBase).IsClass).IsTrue();

        // Verify the two required abstract members exist
        var stringToField = typeof(ConverterBase).GetMethod("StringToField");
        var fieldToString = typeof(ConverterBase).GetMethod("FieldToString");

        await Assert.That(stringToField).IsNotNull();
        await Assert.That(fieldToString).IsNotNull();
    }
}
