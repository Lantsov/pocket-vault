using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using Android.Views;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.Core.View;
using PocketVault.Android.Diagnostics;

namespace PocketVault.Android;

[Activity(Name = "com.lantsov.pocketvault.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private ActivityResultLauncher? documentLauncher;
    private TaskCompletionSource<string?>? documentRequest;
    private NavigationBackCallback? navigationBackCallback;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Keep sensitive content out of screenshots and the recent-apps snapshot by default.
        Window?.AddFlags(WindowManagerFlags.Secure);
#if DEBUG
        // Deliberate, per-launch opt-out for screenshots containing synthetic demo data only.
        // This code (and the intent extra) does not exist in Release builds.
        if (Intent?.GetBooleanExtra("demo_screenshots", false) == true)
            Window?.ClearFlags(WindowManagerFlags.Secure);
        if (Intent?.GetStringExtra("crypto_smoke_run_id") is { Length: > 0 and <= 64 } runId)
            SmokeTestRequest.RunId = runId;
        if (Intent?.GetStringExtra("document_smoke_run_id") is { Length: > 0 and <= 64 } documentRunId)
            SmokeTestRequest.DocumentRunId = documentRunId;
#endif
        base.OnCreate(savedInstanceState);
        if (Window is { } window)
        {
            window.SetSoftInputMode(SoftInput.AdjustResize);
            if (WindowCompat.GetInsetsController(window, window.DecorView) is { } insets)
            {
                insets.AppearanceLightStatusBars = false;
                insets.AppearanceLightNavigationBars = false;
            }
            // Android 15+ draws edge-to-edge; the page supplies dark system safe areas.
            if (!OperatingSystem.IsAndroidVersionAtLeast(35))
            {
                window.SetStatusBarColor(global::Android.Graphics.Color.Rgb(20, 20, 20));
                window.SetNavigationBarColor(global::Android.Graphics.Color.Rgb(20, 20, 20));
            }
        }
        documentLauncher = RegisterForActivityResult(new ActivityResultContracts.StartActivityForResult(),
            new DocumentResultCallback(this));
        navigationBackCallback = new NavigationBackCallback(this);
        OnBackPressedDispatcher.AddCallback(this, navigationBackCallback);
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        ClearSensitiveData();
        base.OnSaveInstanceState(outState);
    }

    internal Task<string?> SelectDocumentAsync(Intent intent)
    {
        if (documentRequest is not null) throw new InvalidOperationException("Диалог уже открыт.");
        var request = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        documentRequest = request;
        try { documentLauncher!.Launch(intent); }
        catch { documentRequest = null; throw; }
        return request.Task;
    }

    protected override void OnDestroy()
    {
        ClearSensitiveData();
        documentRequest?.TrySetCanceled();
        documentRequest = null;
        navigationBackCallback?.Remove();
        navigationBackCallback = null;
        base.OnDestroy();
    }

    private static void ClearSensitiveData()
    {
        var page = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is NavigationPage navigation)
            page = navigation.Navigation.NavigationStack.OfType<MainPage>().FirstOrDefault();
        if (page is MainPage main) main.ClearSensitiveData();
    }

    private sealed class NavigationBackCallback(MainActivity activity) : AndroidX.Activity.OnBackPressedCallback(true)
    {
        private bool isPopping;

        public override async void HandleOnBackPressed()
        {
            if (isPopping) return;
            // The IME consumes Back before this dispatcher. Preserve Android's normal
            // root-page behavior while popping the MAUI settings stack explicitly.
            if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage navigation &&
                navigation.Navigation.NavigationStack.Count > 1)
            {
                isPopping = true;
                try { await navigation.PopAsync(); }
                finally { isPopping = false; }
                return;
            }

            Enabled = false;
            try { activity.OnBackPressedDispatcher.OnBackPressed(); }
            finally { Enabled = true; }
        }
    }

    private sealed class DocumentResultCallback(MainActivity activity) : Java.Lang.Object, IActivityResultCallback
    {
        public void OnActivityResult(Java.Lang.Object? result)
        {
            var request = activity.documentRequest;
            activity.documentRequest = null;
            if (result is ActivityResult response && response.ResultCode == (int)Result.Ok)
            {
                string? id = response.Data?.Data?.ToString();
                if (id is null) request?.TrySetException(new IOException("Провайдер не вернул документ."));
                else request?.TrySetResult(id);
            }
            else request?.TrySetResult(null);
        }
    }
}
