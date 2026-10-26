using Android.App;
using Android.Content.PM;
using Android.OS;
using PocketVault.Android.Diagnostics;

namespace PocketVault.Android;

[Activity(Name = "com.lantsov.pocketvault.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
#if DEBUG
        if (Intent?.GetStringExtra("crypto_smoke_run_id") is { Length: > 0 and <= 64 } runId)
            SmokeTestRequest.RunId = runId;
#endif
        base.OnCreate(savedInstanceState);
    }
}
