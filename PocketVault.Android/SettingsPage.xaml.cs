using PocketVault.Android.Localization;

namespace PocketVault.Android;

public partial class SettingsPage : ContentPage
{
    private UI.SystemBarInsets? systemBarInsets;
    private static LocalizationManager L => LocalizationManager.Current;
    private const string ProjectUrl = "https://github.com/Lantsov/pocket-vault";
    // The project's own MIT notice, kept offline and visible without leaving the app.
    private const string MitLicense = """
        MIT License

        Copyright (c) 2026 Lantsov

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        """;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => AttachSystemBarInsets();
        Unloaded += (_, _) => DetachSystemBarInsets();
        UpdateLanguage();
        LicenseLabel.Text = MitLicense;
        using var fontLicense = typeof(SettingsPage).Assembly.GetManifestResourceStream("PocketVault.Android.FontLicense")!;
        using var reader = new StreamReader(fontLicense);
        FontLicenseLabel.Text = reader.ReadToEnd();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        SettingsContent.WidthRequest = Math.Min(width, 560);
        // A single compact row at normal phone widths; stack at large text sizes.
        double fontScale = global::Android.App.Application.Context.Resources?.Configuration?.FontScale ?? 1;
        double optionWidth = (Math.Min(width, 560) - 48 - 32 - 16) / 3;
        bool stacked = optionWidth < 9 * SystemLanguageButton.FontSize * 0.6 * fontScale + 18;
        LanguageOptions.RowSpacing = stacked ? 8 : 0;
        var options = new[] { SystemLanguageButton, RussianLanguageButton, EnglishLanguageButton };
        for (int i = 0; i < options.Length; i++)
        {
            Grid.SetRow(options[i], stacked ? i : 0);
            Grid.SetColumn(options[i], stacked ? 0 : i);
            Grid.SetColumnSpan(options[i], stacked ? 3 : 1);
        }
        bool stackedFacts = fontScale > 1.2;
        int factRows = stackedFacts ? 4 : 2;
        if (AppFacts.RowDefinitions.Count != factRows)
        {
            AppFacts.RowDefinitions.Clear();
            for (int i = 0; i < factRows; i++)
                AppFacts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        Grid.SetColumn(FormatValue, stackedFacts ? 0 : 1);
        Grid.SetColumn(LimitValue, stackedFacts ? 0 : 1);
        Grid.SetRow(FormatValue, stackedFacts ? 1 : 0);
        Grid.SetRow(LimitCaption, stackedFacts ? 2 : 1);
        Grid.SetRow(LimitValue, stackedFacts ? 3 : 1);
        foreach (var label in new[] { FormatCaption, FormatValue, LimitCaption, LimitValue })
            Grid.SetColumnSpan(label, stackedFacts ? 2 : 1);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private async void OnGitHubClicked(object? sender, EventArgs e)
    {
        GitHubButton.IsEnabled = false;
        LinkErrorLabel.IsVisible = false;
        try
        {
            if (!await Browser.Default.OpenAsync(ProjectUrl, BrowserLaunchMode.External))
                ShowLinkError();
        }
        catch
        {
            ShowLinkError();
        }
        finally { GitHubButton.IsEnabled = true; }
    }

    private void ShowLinkError()
    {
        LinkErrorLabel.Text = L["BrowserFailed"];
        LinkErrorLabel.IsVisible = true;
    }

    private void OnLicenseClicked(object? sender, EventArgs e)
    {
        LicensePanel.IsVisible = !LicensePanel.IsVisible;
        UpdateLanguage();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(AttachSystemBarInsets);
        L.LanguageChanged += OnLanguageChanged;
        L.RefreshSystemLanguage();
        UpdateLanguage();
#if DEBUG
        Diagnostics.TypographyAudit.Schedule(this, "settings");
#endif
    }

    protected override void OnDisappearing()
    {
        DetachSystemBarInsets();
        L.LanguageChanged -= OnLanguageChanged;
        base.OnDisappearing();
    }

    private void OnLanguageClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string choice }) L.Select(choice);
    }

    private void AttachSystemBarInsets()
    {
        if (systemBarInsets is null && Handler?.PlatformView is global::Android.Views.View)
            systemBarInsets = new UI.SystemBarInsets(this, PageLayout);
    }

    private void DetachSystemBarInsets()
    {
        systemBarInsets?.Detach();
        systemBarInsets = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateLanguage();

    private void UpdateLanguage()
    {
        VersionLabel.Text = L.Format("VersionFormat", AppInfo.Current.VersionString, AppInfo.Current.BuildString);
        foreach (var (button, choice) in new[] { (SystemLanguageButton, "system"), (RussianLanguageButton, "ru"), (EnglishLanguageButton, "en") })
        {
            bool selected = L.Choice == choice;
            button.BorderColor = Color.FromArgb(selected ? "#F5F5F5" : "#353535");
            button.BackgroundColor = Color.FromArgb(selected ? "#353535" : "#1E1E1E");
            SemanticProperties.SetHint(button, selected ? L["LanguageSelected"] : L["LanguageHint"]);
        }
        if (LinkErrorLabel.IsVisible) LinkErrorLabel.Text = L["BrowserFailed"];
        LicenseButton.Text = L[LicensePanel.IsVisible ? "HideMitLicense" : "MitLicense"];
        FontLicenseButton.Text = L[FontLicensePanel.IsVisible ? "HideFontLicense" : "FontLicense"];
        SemanticProperties.SetHint(LicenseButton, L[LicensePanel.IsVisible ? "HideLicenseHint" : "ShowLicenseHint"]);
        SemanticProperties.SetHint(FontLicenseButton, L[FontLicensePanel.IsVisible ? "HideLicenseHint" : "ShowLicenseHint"]);
    }

    private void OnFontLicenseClicked(object? sender, EventArgs e)
    {
        FontLicensePanel.IsVisible = !FontLicensePanel.IsVisible;
        UpdateLanguage();
    }
}
