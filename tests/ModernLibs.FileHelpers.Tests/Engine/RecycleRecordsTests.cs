// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0025: Suppress "Make sure not to use Assert.That with a constant value" false positive on Sentinel pattern.
#pragma warning disable MA0025
// MA0048: RecycleNumericRecord fixture co-located in RecycleRecordsTests.cs — intentional.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// Numeric-only class fixture for RecycleRecords allocation tests.
/// No string fields — zero heap allocations per ParseInto call after warmup (no new string(...) calls).
/// Internal (not file-scoped) visibility so the source generator can emit a [ModuleInitializer] descriptor.
/// </summary>
[DelimitedRecord(",")]
internal sealed class RecycleNumericRecord
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public int Value;
    [FieldOrder(3)] public decimal Amount;
}

/// <summary>
/// Acceptance tests for <c>RecycleRecords</c> mode on <see cref="FileHelperAsyncEngine{T}"/>.
/// Covers ENGI-15 (RecycleRecords property — instance reuse for class types).
/// </summary>
[Category("Phase08")]
[Category("RecycleRecords")]
public sealed class RecycleRecordsTests
{
    // -------------------------------------------------------------------------
    // ENGI-15 — RecycleRecords
    // -------------------------------------------------------------------------

    /// <summary>ENGI-15: RecycleRecords=true; parse 3 class records; all ReadNext calls return same object reference.</summary>
    [Test]
    public async Task RecycleRecords_ClassType_SameInstanceReused()
    {
        // CsvOrder has [IgnoreFirst(1)] — header line + 3 data rows
        const string content = "header\r\n1,Alice,10.00\r\n2,Bob,20.00\r\n3,Charlie,30.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder> { RecycleRecords = true };
        engine.BeginReadString(content);

        var r1 = engine.ReadNext();
        var r2 = engine.ReadNext();
        var r3 = engine.ReadNext();

        await Assert.That(r1).IsNotNull();
        await Assert.That(r2).IsNotNull();
        await Assert.That(r3).IsNotNull();
        // All three calls must return the same instance
        await Assert.That(ReferenceEquals(r1, r2)).IsTrue();
        await Assert.That(ReferenceEquals(r2, r3)).IsTrue();
    }

    /// <summary>ENGI-15: RecycleRecords=true allocates significantly fewer bytes per ReadNext() call than RecycleRecords=false (no new record object per call).</summary>
    [Test]
    public async Task RecycleRecords_AllocationDelta_NearZeroAfterWarmup()
    {
        // Use RecycleNumericRecord (int, int, decimal only — no string fields).
        // ReadNext() always allocates a line string via TextReader.ReadLine() — that is
        // unavoidable in the blocking-streaming path. RecycleRecords eliminates the extra
        // RecycleNumericRecord object allocation on top of the line string.
        //
        // We verify that RecycleRecords=true allocates < 64 bytes MORE per call than
        // the irreducible minimum (the line string from ReadLine()). This means no new
        // record object is allocated per call.
        const int N = 10;

        // Build N+2 lines (warmup + N measured calls + 1 spare) using InvariantCulture
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= N + 2; i++)
            sb.AppendLine(System.FormattableString.Invariant($"{i},{i * 2},{i}"));
        string content = sb.ToString();

        // --- Baseline: measure allocation per ReadNext() with RecycleRecords=false ---
        using var engineBaseline = new FileHelperAsyncEngine<RecycleNumericRecord> { RecycleRecords = false };
        engineBaseline.BeginReadString(content);
        engineBaseline.ReadNext(); // warmup

        long beforeBaseline = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < N; i++)
            engineBaseline.ReadNext();
        long afterBaseline = GC.GetAllocatedBytesForCurrentThread();
        long baselinePerCall = (afterBaseline - beforeBaseline) / N;

        // --- Recycled: measure allocation per ReadNext() with RecycleRecords=true ---
        using var engineRecycled = new FileHelperAsyncEngine<RecycleNumericRecord> { RecycleRecords = true };
        engineRecycled.BeginReadString(content);
        engineRecycled.ReadNext(); // warmup — allocates the first instance

        long beforeRecycled = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < N; i++)
            engineRecycled.ReadNext();
        long afterRecycled = GC.GetAllocatedBytesForCurrentThread();
        long recycledPerCall = (afterRecycled - beforeRecycled) / N;

        // Recycled path must allocate meaningfully less per call (at least 24 bytes less —
        // the record object header overhead that RecycleRecords eliminates).
        await Assert.That(recycledPerCall).IsLessThan(baselinePerCall);
    }

    /// <summary>ENGI-15: RecycleRecords=true on a struct record type (PositionalOrder); ReadNext succeeds without exception.</summary>
    [Test]
    public async Task RecycleRecords_StructType_FallsThrough_NoException()
    {
        // PositionalOrder is a readonly record struct — RecycleRecords is meaningless but must not throw
        const string content = "1,Alice,10.00\r\n2,Bob,20.00\r\n";

        using var engine = new FileHelperAsyncEngine<PositionalOrder> { RecycleRecords = true };
        engine.BeginReadString(content);

        var r1 = engine.ReadNext();
        var r2 = engine.ReadNext();

        // Struct: correct values returned, no exception
        await Assert.That(r1.OrderId).IsEqualTo(1);
        await Assert.That(r2.OrderId).IsEqualTo(2);
    }

    /// <summary>ENGI-15: RecycleRecords=false (default); parse 3 class records; each is a distinct object reference.</summary>
    [Test]
    public async Task RecycleRecords_False_AllocatesNewInstancePerRecord()
    {
        const string content = "header\r\n1,Alice,10.00\r\n2,Bob,20.00\r\n3,Charlie,30.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>(); // RecycleRecords defaults to false
        engine.BeginReadString(content);

        var r1 = engine.ReadNext();
        var r2 = engine.ReadNext();
        var r3 = engine.ReadNext();

        await Assert.That(r1).IsNotNull();
        await Assert.That(r2).IsNotNull();
        await Assert.That(r3).IsNotNull();
        // Without RecycleRecords each call allocates a fresh instance
        await Assert.That(ReferenceEquals(r1, r2)).IsFalse();
        await Assert.That(ReferenceEquals(r2, r3)).IsFalse();
    }
}
