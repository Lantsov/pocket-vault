using Android.Views;
using AndroidX.Core.View;

namespace PocketVault.Android.UI;

// Some Android/MAUI window combinations report the full page height under the IME
// even with AdjustResize. Constrain only the scroll viewport to the visible frame.
internal sealed class KeyboardViewport : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
{
    private readonly ScrollView scroll;
    private readonly ContentPage page;
    private readonly Func<Entry?> focusedEntry;
    private readonly global::Android.Views.View native;
    private readonly global::Android.Views.View root;
    private double lastHeight = -1;

    internal KeyboardViewport(ContentPage page, ScrollView scroll, Func<Entry?> focusedEntry)
    {
        this.page = page;
        this.scroll = scroll;
        this.focusedEntry = focusedEntry;
        native = (global::Android.Views.View)scroll.Handler!.PlatformView!;
        root = native.RootView!;
        root.ViewTreeObserver!.AddOnGlobalLayoutListener(this);
    }

    public void OnGlobalLayout()
    {
        using var visible = new global::Android.Graphics.Rect();
        root.GetWindowVisibleDisplayFrame(visible);
        double density = root.Resources!.DisplayMetrics!.Density;
        // Visible-frame differences can also occur briefly during rotation/resizing.
        // Use Android's IME insets to distinguish the keyboard from those changes.
        bool keyboard = ViewCompat.GetRootWindowInsets(native)?.IsVisible(WindowInsetsCompat.Type.Ime()) == true;
        double height = -1;
        if (keyboard)
        {
            var location = new int[2];
            native.GetLocationOnScreen(location);
            height = Math.Max(48, Math.Min(page.Height, (visible.Bottom - location[1]) / density));
        }
        if (Math.Abs(lastHeight - height) < 1) return;
        lastHeight = height;
        scroll.VerticalOptions = keyboard ? LayoutOptions.Start : LayoutOptions.Fill;
        scroll.HeightRequest = height;
        if (keyboard)
            scroll.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), async () =>
            {
                if (lastHeight > 0 && focusedEntry() is { } entry)
                    await scroll.ScrollToAsync(entry, ScrollToPosition.MakeVisible, false);
            });
    }

    internal void Detach()
    {
        if (root.ViewTreeObserver?.IsAlive == true)
            root.ViewTreeObserver.RemoveOnGlobalLayoutListener(this);
        lastHeight = -1;
        scroll.HeightRequest = -1;
        scroll.VerticalOptions = LayoutOptions.Fill;
        Dispose();
    }
}
