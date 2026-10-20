// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>3.x <c>[IgnoreCommentedLines(marker, anyPlace)]</c>: anyPlace (default) allows blanks before the marker.</summary>
[Category("Generator")]
public sealed class CommentAnyPlaceTests
{
    [Test]
    [DisplayName("3.x: anyPlace=true (default) skips comments preceded by blanks; anyPlace=false only at column 0")]
    public async Task AnyPlace_ControlsLeadingBlanks()
    {
        // Arrange
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] [IgnoreCommentedLines('#')] public sealed class A { public string X = ""; public int Y; }
            [DelimitedRecord(",")] [IgnoreCommentedLines('#', false)] public sealed class B { public string X = ""; public int Y; }
            public static class Probe
            {
                public static string Run()
                {
                    const string input = "a,1\n \t# c,2\n#x,3\nb,4";
                    var a = new FileHelperEngine<A>().ReadString(input);
                    var b = new FileHelperEngine<B>().ReadString(input);
                    return a.Length + "|" + b.Length + "|[" + b[1].X + "]|" + FileHelperRegistry<A>.Descriptor.Options.CommentAnyPlace + "|" + FileHelperRegistry<B>.Descriptor.Options.CommentAnyPlace;
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert: A skips both comment lines; B keeps the indented one as data
        await Assert.That(output).IsEqualTo("2|3|[ \t# c]|True|False");
    }
}
