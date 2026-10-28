namespace PocketVault.Android;

public partial class App : Application
{
    public App()
    {
        _ = Localization.LocalizationManager.Current;
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(new MainPage()) { BarBackgroundColor = Color.FromArgb("#141414") });
}
