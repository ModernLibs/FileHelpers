// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// VG3: records with C# 11 <c>required</c> members. The generated code must satisfy every required member
/// (CS9035) in Parse and in CreateDefaultInstance, unless the constructor it calls has
/// <c>[SetsRequiredMembers]</c>.
/// </summary>
[Category("Generator")]
public sealed class RequiredMemberTests
{
    [Test]
    [DisplayName("VG3: required fields and properties are read and written")]
    public async Task RequiredMembers_RoundTrip()
    {
        // Arrange: required property and field, plus a hidden required member that must still be set
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R : INotifyRead<R>
            {
                public required string Name { get; set; }
                public required int Id;
                [FieldHidden] public required string Note { get; init; }
                public void BeforeRead(BeforeReadEventArgs<R> e) { }
                public void AfterRead(AfterReadEventArgs<R> e) { }
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var r = engine.ReadString("x,7")[0];
                    return r.Name + "|" + r.Id + "|" + (r.Note ?? "null") + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("x|7|null|x,7");
    }

    [Test]
    [DisplayName("VG3: a required member bound to a constructor parameter is also set in the initializer")]
    public async Task RequiredMember_CtorBound_Compiles()
    {
        // Arrange: no parameterless constructor, so the constructor is used; A is required
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R
            {
                public R(string a) { A = a; }
                public required string A { get; init; }
                public int B { get; set; }
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("x,3")[0];
                    return r.A + "|" + r.B;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("x|3");
    }

    [Test]
    [DisplayName("VG3: a [SetsRequiredMembers] constructor is not followed by required initializers")]
    public async Task SetsRequiredMembers_Constructor_Compiles()
    {
        // Arrange: the constructor sets the hidden required member itself
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R
            {
                [SetsRequiredMembers]
                public R() { Tag = "ctor"; }
                public string Name { get; set; } = "";
                [FieldHidden] public required string Tag { get; set; }
            }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("x")[0];
                    return r.Name + "|" + r.Tag;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert: the constructor's value is kept
        await Assert.That(output).IsEqualTo("x|ctor");
    }

    [Test]
    [DisplayName("VG3: a struct record with required members compiles")]
    public async Task RequiredMembers_Struct_Compiles()
    {
        // Arrange
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public struct S
            {
                [FieldFixedLength(2)] public required int Id;
                [FieldFixedLength(3)] public required string Code { get; set; }
            }
            public static class Probe
            {
                public static string Run()
                {
                    var s = new FileHelperEngine<S>().ReadString("07abc")[0];
                    return s.Id + "|" + s.Code;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("7|abc");
    }
}
