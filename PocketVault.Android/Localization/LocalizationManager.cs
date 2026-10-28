using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace PocketVault.Android.Localization;

// The indexer bindings update existing pages; no navigation state or passwords are copied.
public sealed class LocalizationManager : INotifyPropertyChanged
{
    private const string PreferenceKey = "ui.language";
    private readonly ResourceManager resources = new("PocketVault.Android.Resources.Localization.AppResources", typeof(LocalizationManager).Assembly);
    public static LocalizationManager Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;
    public string Choice { get; private set; }
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");
    public string this[string key] => resources.GetString(key, Culture) ?? throw new ArgumentException($"Missing UI resource: {key}");
    public string Format(string key, params object[] args) => string.Format(Culture, this[key], args);

    private LocalizationManager()
    {
        Choice = Preferences.Default.Get(PreferenceKey, "system");
        if (Choice is not ("ru" or "en")) Choice = "system";
        ApplyCulture();
    }

    public void Select(string choice)
    {
        if (choice is not ("system" or "ru" or "en")) throw new ArgumentOutOfRangeException(nameof(choice));
        Choice = choice;
        Preferences.Default.Set(PreferenceKey, choice);
        ApplyCulture();
        NotifyLanguageChanged();
    }

    public void RefreshSystemLanguage()
    {
        var previous = Culture;
        ApplyCulture();
        if (previous.Name != Culture.Name) NotifyLanguageChanged();
    }

    private void NotifyLanguageChanged()
    {
        // MAUI indexer bindings must also refresh their leaf value on both pages.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyCulture()
    {
        string language = Choice == "system" ? Java.Util.Locale.Default?.Language ?? "en" : Choice;
        Culture = CultureInfo.GetCultureInfo(language == "ru" ? "ru" : "en");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }
}
