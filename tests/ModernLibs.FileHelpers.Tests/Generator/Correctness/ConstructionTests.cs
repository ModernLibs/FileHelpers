// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// How the generated descriptor constructs and assigns record instances.
/// </summary>
[Category("Generator")]
public sealed class ConstructionTests
{
    [Test]
    [DisplayName("Positional record struct with reversed FieldOrder binds values by parameter name (swap repro)")]
    public async Task PositionalRecordStruct_ReversedOrder_BindsByName()
    {
        // Arrange: the review repro — A is column 2, B is column 1
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public readonly record struct Swap([property: FieldOrder(2)] int A, [property: FieldOrder(1)] int B);
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<Swap>();
                    var r = engine.ReadString("1,2")[0];
                    return r.A + "|" + r.B + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: column 1 -> B, column 2 -> A; writing restores the column order
        await Assert.That(output).IsEqualTo("2|1|1,2");
    }

    [Test]
    [DisplayName("Positional record class (no parameterless constructor) is constructed through its primary constructor")]
    public async Task PositionalRecordClass_UsesPrimaryConstructor()
    {
        // Arrange: positional record class
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed record class Person(string Name, int Age);
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<Person>();
                    var r = engine.ReadString("Ann,41")[0];
                    return r.Name + "|" + r.Age + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: values bound and written back
        await Assert.That(output).IsEqualTo("Ann|41|Ann,41");
    }

    [Test]
    [DisplayName("Class without a parameterless constructor binds constructor parameters by name, case-insensitively")]
    public async Task ClassWithoutDefaultCtor_BindsParametersByName()
    {
        // Arrange: get-only properties set through a constructor whose parameter order differs
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Account
            {
                public Account(string name, int id) { Name = name; Id = id; }
                public int Id { get; }
                public string Name { get; }
                public string Computed => Name + Id;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<Account>();
                    var r = engine.ReadString("7,Bob")[0];
                    return r.Id + "|" + r.Name + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: Id/Name bound by name; computed property skipped
        await Assert.That(output).IsEqualTo("7|Bob|7,Bob");
    }

    [Test]
    [DisplayName("Struct with init-only properties is constructed with an object initializer")]
    public async Task InitOnlyStruct_UsesObjectInitializer()
    {
        // Arrange: non-positional struct with init-only properties
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public struct Point
            {
                public int X { get; init; }
                public int Y { get; init; }
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<Point>().ReadString("3,4")[0];
                    return r.X + "|" + r.Y;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: both values set
        await Assert.That(output).IsEqualTo("3|4");
    }

    [Test]
    [DisplayName("[FieldValueDiscarded] constructor parameter receives default and keeps argument count")]
    public async Task DiscardedConstructorParameter_ReceivesDefault()
    {
        // Arrange: positional record class with a discarded middle column
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed record class D(int A, [property: FieldValueDiscarded] string? B, int C);
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<D>().ReadString("1,x,3")[0];
                    return r.A + "|" + (r.B ?? "null") + "|" + r.C;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: discarded value not stored, other columns intact
        await Assert.That(output).IsEqualTo("1|null|3");
    }

    [Test]
    [DisplayName("Get-only, private-set and readonly members are skipped instead of breaking compilation")]
    public async Task UnassignableMembers_AreSkipped()
    {
        // Arrange: class mixing assignable and unassignable public members
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Mixed
            {
                public string Name = "";
                public int ReadOnlyProp { get; } = 5;
                public int PrivateSet { get; private set; }
                public readonly int ReadOnlyField = 9;
                public int Age;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<Mixed>().ReadString("Zed,30")[0];
                    return r.Name + "|" + r.Age;
                }
            }
            """;

        // Act: compile and execute
        var run = GeneratorHarness.RunGenerator(source);
        string output = GeneratorHarness.Execute(source);

        // Assert: only Name and Age are mapped; no diagnostics for unannotated members
        await Assert.That(output).IsEqualTo("Zed|30");
        await Assert.That(run.Diagnostics("MLFH015").Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("An annotated member that cannot be assigned reports MLFH015")]
    public async Task AnnotatedUnassignableMember_ReportsDiagnostic()
    {
        // Arrange: [FieldOrder] on a get-only property with no constructor binding
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Bad
            {
                [FieldOrder(1)] public string Name { get; set; } = "";
                [FieldOrder(2)] public int Total { get; } = 0;
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH015 at the member
        var diagnostics = run.Diagnostics("MLFH015");
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Location.IsInSource).IsTrue();
        await Assert.That(diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("Total");
    }

    [Test]
    [DisplayName("Abstract record type reports MLFH014 (no usable construction strategy)")]
    public async Task AbstractRecord_ReportsDiagnostic()
    {
        // Arrange: abstract class annotated as a record
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public abstract class Shape { public string Name = ""; }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH014 reported, nothing generated, compilation clean
        await Assert.That(run.Diagnostics("MLFH014").Count).IsEqualTo(1);
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("Constructor parameter without a matching member reports MLFH014")]
    public async Task UnbindableConstructor_ReportsDiagnostic()
    {
        // Arrange: only constructor takes a parameter that maps to no member
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Odd
            {
                public Odd(int seed) { Name = seed.ToString(); }
                public string Name { get; }
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH014 reported, compilation clean
        await Assert.That(run.Diagnostics("MLFH014").Count).IsEqualTo(1);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("Nested record types are referenced through their containing type chain")]
    public async Task NestedRecord_Works()
    {
        // Arrange: record nested two levels deep
        const string source = """
            using ModernLibs.FileHelpers;
            namespace Shop
            {
                public static class Outer
                {
                    internal static class Middle
                    {
                        [FixedLengthRecord]
                        internal sealed class Line { [FieldFixedLength(3)] public string Code = ""; [FieldFixedLength(2)] public int Qty; }
                    }
                }
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<Shop.Outer.Middle.Line>().ReadString("ABC12")[0];
                    return r.Code + "|" + r.Qty;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: parsed through the nested type
        await Assert.That(output).IsEqualTo("ABC|12");
    }

    [Test]
    [DisplayName("A private nested record type reports MLFH017")]
    public async Task PrivateNestedRecord_ReportsDiagnostic()
    {
        // Arrange: record type not accessible outside its containing type
        const string source = """
            using ModernLibs.FileHelpers;
            public sealed class Host
            {
                [DelimitedRecord(",")] private sealed class Hidden { public string Name = ""; }
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH017 reported, compilation clean
        await Assert.That(run.Diagnostics("MLFH017").Count).IsEqualTo(1);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("Record nested in a generic type reports MLFH016")]
    public async Task RecordInGenericType_ReportsDiagnostic()
    {
        // Arrange: non-generic record inside a generic containing type
        const string source = """
            using ModernLibs.FileHelpers;
            public sealed class Box<T>
            {
                [DelimitedRecord(",")] public sealed class Item { public string Name = ""; }
            }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH016 reported, compilation clean
        await Assert.That(run.Diagnostics("MLFH016").Count).IsEqualTo(1);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG9: INotifyRead on a record with a validating constructor does not call it with null arguments")]
    public async Task ValidatingConstructor_WithNotifyRead_Reads()
    {
        // Arrange: no parameterless constructor; the constructor rejects null
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R : INotifyRead<R>
            {
                public R(string name, int id)
                {
                    ArgumentNullException.ThrowIfNull(name);
                    Name = name;
                    Id = id;
                }
                public string Name { get; }
                public int Id { get; }
                public void BeforeRead(BeforeReadEventArgs<R> e) { }
                public void AfterRead(AfterReadEventArgs<R> e) { }
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("x,1")[0];
                    return r.Name + "|" + r.Id;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("x|1");
    }

    [Test]
    [DisplayName("VG9: a constructor-built record with a parameterless constructor uses it for the default instance")]
    public async Task ConstructorBuilt_WithParameterless_DefaultInstanceUsesIt()
    {
        // Arrange: Id is get-only (constructor-bound), the parameterless constructor sets a marker
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R
            {
                public R() { Id = -1; }
                public R(int id) { Id = id; }
                public int Id { get; }
            }
            public static class Probe
            {
                public static string Run() =>
                    FileHelperRegistry<R>.Descriptor.CreateDefaultInstance().Id + "|" + new FileHelperEngine<R>().ReadString("5")[0].Id;
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("-1|5");
    }
}
