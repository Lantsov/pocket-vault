namespace PocketVault.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping("PlexTypography", (handler, _) =>
        {
            handler.PlatformView.SetIncludeFontPadding(false);
        });
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("PocketVaultEntry", (handler, _) =>
        {
            // Passwords belong to this view's lifetime, never Android's saved view state/autofill.
            handler.PlatformView.SaveEnabled = false;
            handler.PlatformView.SaveFromParentEnabled = false;
            handler.PlatformView.ImportantForAutofill = global::Android.Views.ImportantForAutofill.NoExcludeDescendants;
            handler.PlatformView.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(
                global::Android.Graphics.Color.Transparent);
        });
        // Let large RU/EN labels wrap instead of Android's default one-line button.
        Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("PocketVaultButton", (handler, _) =>
        {
            handler.PlatformView.SetSingleLine(false);
            handler.PlatformView.SetHorizontallyScrolling(false);
            handler.PlatformView.Ellipsize = null;
        });
        return MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("IBMPlexMono-Regular.ttf", "IBMPlexMonoRegular");
                fonts.AddFont("IBMPlexMono-Medium.ttf", "IBMPlexMonoMedium");
                fonts.AddFont("IBMPlexMono-SemiBold.ttf", "IBMPlexMonoSemiBold");
            })
            .Build();
    }
}
