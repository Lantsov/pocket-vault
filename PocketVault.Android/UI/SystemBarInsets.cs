using Android.Views;
using AndroidX.Core.View;

namespace PocketVault.Android.UI;

// Reserve actual Android system bars/cutouts for edge-to-edge MAUI layouts.
// The page opts out of automatic insets to avoid applying the same space twice.
internal sealed class SystemBarInsets : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
{
    private readonly Grid layout;
    private readonly global::Android.Views.View root;

    internal SystemBarInsets(ContentPage page, Grid layout)
    {
        this.layout = layout;
        root = ((global::Android.Views.View)page.Handler!.PlatformView!).RootView!;
        root.ViewTreeObserver!.AddOnGlobalLayoutListener(this);
        OnGlobalLayout();
    }

    public void OnGlobalLayout()
    {
        var insets = ViewCompat.GetRootWindowInsets(root);
        if (insets is null) return;
        var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
        if (bars is null) return;
        double density = root.Resources!.DisplayMetrics!.Density;
        var padding = new Thickness(bars.Left / density, bars.Top / density, bars.Right / density, bars.Bottom / density);
        if (layout.Padding != padding) layout.Padding = padding;
    }

    internal void Detach()
    {
        if (root.ViewTreeObserver?.IsAlive == true)
            root.ViewTreeObserver.RemoveOnGlobalLayoutListener(this);
        Dispose();
    }
}
