using System.Text.Json.Serialization;

namespace PocketVault.Android.Diagnostics;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SmokeReport))]
internal partial class SmokeReportJsonContext : JsonSerializerContext;
