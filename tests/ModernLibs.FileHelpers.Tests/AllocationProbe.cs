// Shared allocation-measurement helper for the allocation tests.
namespace ModernLibs.FileHelpers.Tests;

/// <summary>
/// Measures managed allocation on the current thread over several windows and returns the smallest.
/// A single window can include a one-off cost that is not the code under test (an allocation-context refill, or a
/// GC started by a test running in parallel, shows up as about 8 KB). A real allocation in the measured code
/// appears in every window, so the minimum still catches it while one noisy window cannot fail a test.
/// </summary>
internal static class AllocationProbe
{
    /// <summary>Default number of windows measured.</summary>
    public const int Windows = 5;

    /// <summary>Runs <paramref name="window"/> <paramref name="windows"/> times; returns the smallest allocated-bytes delta.</summary>
    /// <remarks>The caller warms up first. <paramref name="window"/> must do the same work on every call.</remarks>
    public static long MinAllocated(Action window, int windows = Windows)
    {
        long min = long.MaxValue;
        for (int w = 0; w < windows; w++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            window();
            long delta = GC.GetAllocatedBytesForCurrentThread() - before;
            if (delta < min)
                min = delta;
        }

        return min;
    }
}
