using PocketVault.Android.Localization;

namespace PocketVault.Android;

public partial class SettingsPage : ContentPage
{
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
        L.LanguageChanged += OnLanguageChanged;
        L.RefreshSystemLanguage();
        UpdateLanguage();
#if DEBUG
        Diagnostics.TypographyAudit.Schedule(this, "settings");
#endif
    }

    protected override void OnDisappearing()
    {
        L.LanguageChanged -= OnLanguageChanged;
        base.OnDisappearing();
    }

    private void OnLanguageClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string choice }) L.Select(choice);
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
