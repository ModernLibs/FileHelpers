// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using ModernLibs.FileHelpers.Fuzz.Tests;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Fuzz;

/// <summary>The extended fuzz runner exits 0 or 1, never with the failure count (a count of 256 reads as 0 on Linux).</summary>
[Category("Fuzz")]
public sealed class FuzzExitTests
{
    [Test]
    public async Task No_Failure_Exits_Zero()
    {
        await Assert.That(FuzzExit.FromFailureCount(0)).IsEqualTo(0);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(255)]
    [Arguments(256)]
    [Arguments(512)]
    [Arguments(int.MaxValue)]
    public async Task Any_Failure_Exits_One(int failures)
    {
        await Assert.That(FuzzExit.FromFailureCount(failures)).IsEqualTo(1);
    }
}
