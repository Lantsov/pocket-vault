namespace PocketVault.Android.Diagnostics;

internal static class SmokeTestRequest
{
    internal static string? RunId { get; set; }
#if DEBUG
    internal static string? DocumentRunId { get; set; }
#endif
}
