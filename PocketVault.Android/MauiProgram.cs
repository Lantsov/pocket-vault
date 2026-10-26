namespace PocketVault.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("PocketVaultEntry", (handler, _) =>
        {
            // Passwords belong to this view's lifetime, never Android's saved view state/autofill.
            handler.PlatformView.SaveEnabled = false;
            handler.PlatformView.SaveFromParentEnabled = false;
            handler.PlatformView.ImportantForAutofill = global::Android.Views.ImportantForAutofill.NoExcludeDescendants;
            handler.PlatformView.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(
                global::Android.Graphics.Color.Transparent);
        });
        return MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .Build();
    }
}
