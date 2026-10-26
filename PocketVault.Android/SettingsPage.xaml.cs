namespace PocketVault.Android;

public partial class SettingsPage : ContentPage
{
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
        VersionLabel.Text = $"Версия {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        LicenseLabel.Text = MitLicense;
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
        LinkErrorLabel.Text = "Не удалось открыть браузер. Адрес проекта: github.com/Lantsov/pocket-vault";
        LinkErrorLabel.IsVisible = true;
    }

    private void OnLicenseClicked(object? sender, EventArgs e)
    {
        LicensePanel.IsVisible = !LicensePanel.IsVisible;
        LicenseButton.Text = LicensePanel.IsVisible ? "Скрыть лицензию MIT" : "Лицензия MIT";
        SemanticProperties.SetHint(LicenseButton, LicensePanel.IsVisible
            ? "Скрыть полный текст лицензии" : "Показать полный текст лицензии на этом экране");
    }
}
