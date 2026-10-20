// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// Tests verifying IgnoreInheritedClass attribute functionality.
/// </summary>
[Category("Engine")]
[Category("IgnoreInheritedClass")]
public sealed class IgnoreInheritedClassTests
{
    /// <summary>Inherited fields included when IgnoreInheritedClass absent.</summary>
    [Test]
    [DisplayName("Inherited fields included when IgnoreInheritedClass absent")]
    public async Task InheritedFields_Included_WhenAttributeAbsent()
    {
        // Arrange: Create engine for derived type without IgnoreInheritedClass
        var engine = new FileHelperEngine<DerivedWithInherited>();
        var input = "1,base,derived";

        // Act: Parse input
        var records = engine.ReadString(input);

        // Assert: All three fields parsed (Id, BaseField, DerivedField)
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(records[0].BaseField).IsEqualTo("base");
        await Assert.That(records[0].DerivedField).IsEqualTo("derived");

        // Assert: Descriptor has 3 fields
        var descriptor = FileHelperRegistry<DerivedWithInherited>.Descriptor;
        await Assert.That(descriptor.Fields.Length).IsEqualTo(3);
    }

    /// <summary>Inherited fields excluded when IgnoreInheritedClass present.</summary>
    [Test]
    [DisplayName("Inherited fields excluded when IgnoreInheritedClass present")]
    public async Task InheritedFields_Excluded_WhenAttributePresent()
    {
        // Arrange: Create engine for derived type with IgnoreInheritedClass
        var engine = new FileHelperEngine<DerivedWithoutInherited>();
        var input = "derived";

        // Act: Parse input (only DerivedField expected)
        var records = engine.ReadString(input);

        // Assert: Only DerivedField parsed
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].DerivedField).IsEqualTo("derived");

        // Assert: Descriptor has 1 field
        var descriptor = FileHelperRegistry<DerivedWithoutInherited>.Descriptor;
        await Assert.That(descriptor.Fields.Length).IsEqualTo(1);
        await Assert.That(descriptor.Fields[0].Name).IsEqualTo("DerivedField");
    }

    /// <summary>WriteString outputs only derived fields with IgnoreInheritedClass.</summary>
    [Test]
    [DisplayName("WriteString outputs only derived fields with IgnoreInheritedClass")]
    public async Task WriteString_OnlyDerivedFields_WithIgnoreInheritedClass()
    {
        // Arrange: Create engine for derived type with IgnoreInheritedClass
        var engine = new FileHelperEngine<DerivedWithoutInherited>();
        var records = new DerivedWithoutInherited[]
        {
            new() { DerivedField = "test" },
        };

        // Act: Write string
        var result = engine.WriteString(records);

        // Assert: Output contains only DerivedField
        await Assert.That(result).IsEqualTo("test\r\n");
    }

    /// <summary>IgnoreInheritedClass works through IFileHelperEngine interface.</summary>
    [Test]
    [DisplayName("IgnoreInheritedClass works through IFileHelperEngine interface")]
    public async Task IgnoreInheritedClass_Works_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperEngine<DerivedWithoutInherited> engine = new FileHelperEngine<DerivedWithoutInherited>();
        var input = "hello";

        // Act: Read through interface
        var records = engine.ReadString(input);

        // Assert: Correct record parsed
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].DerivedField).IsEqualTo("hello");
    }
}
