#if DEBUG
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketVault.Android.Diagnostics;

// Synthetic screenshot sessions only. No text, password or document URI is collected.
internal static class TypographyAudit
{
    internal static void Schedule(ContentPage page, string name)
    {
        if (Platform.CurrentActivity?.Intent?.GetBooleanExtra("demo_screenshots", false) != true) return;
        page.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () =>
        {
            var fonts = page.Handler!.MauiContext!.Services.GetRequiredService<IFontManager>();
            var rows = Descendants(page).OfType<VisualElement>()
                .Where(view => view.IsVisible && view is ITextStyle && view.Handler?.PlatformView is global::Android.Widget.TextView)
                .Select(view =>
                {
                    var native = (global::Android.Widget.TextView)view.Handler!.PlatformView!;
                    var style = (ITextStyle)view;
                    return new TypographyRow(view.AutomationId ?? view.GetType().Name, style.Font.Family,
                        Equals(native.Typeface, fonts.GetTypeface(style.Font)),
                        native.Paint?.HasGlyph("Ж") == true && native.Paint.HasGlyph("A"),
                        view is Entry { IsPassword: true },
                        native.TransformationMethod is global::Android.Text.Method.PasswordTransformationMethod);
                }).ToArray();
            File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, $"ui-typography-{name}.json"),
                JsonSerializer.Serialize(rows, TypographyJsonContext.Default.TypographyRowArray));
        });
    }

    private static IEnumerable<IVisualTreeElement> Descendants(IVisualTreeElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}

internal sealed record TypographyRow(string Control, string? Family, bool NativeTypefaceMatches,
    bool LatinAndCyrillicGlyphs, bool PasswordEntry, bool PlatformPasswordMask);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TypographyRow[]))]
internal partial class TypographyJsonContext : JsonSerializerContext;
#endif
