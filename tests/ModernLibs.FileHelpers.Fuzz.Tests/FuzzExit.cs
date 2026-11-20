namespace ModernLibs.FileHelpers.Fuzz.Tests;

/// <summary>
/// The process exit code of a fuzz run. Also compiled into <c>ModernLibs.FileHelpers.Tests</c> (a linked source
/// file), which tests it.
/// </summary>
public static class FuzzExit
{
    /// <summary>
    /// 0 when no input failed, 1 otherwise. The failure count is not an exit code: on Linux a process exit code is
    /// the count modulo 256, so 256 failures would read as success.
    /// </summary>
    public static int FromFailureCount(int failures) => failures == 0 ? 0 : 1;
}
