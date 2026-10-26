using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PocketVault.Core;

namespace PocketVault.App;

internal interface IVaultDialogs
{
    string? SelectFile(Window owner);
    string? SelectDestination(Window owner, string input, bool encrypt);
    bool ConfirmDeletion(Window owner);
}

internal sealed class VaultDialogs : IVaultDialogs
{
    public string? SelectFile(Window owner)
    {
        var dialog = new OpenFileDialog { Title = "Выберите файл", Filter = "Все файлы|*.*|Контейнер PocketVault|*.pvlt", CheckFileExists = true, Multiselect = false };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? SelectDestination(Window owner, string input, bool encrypt)
    {
        if (encrypt)
        {
            var save = new SaveFileDialog { Title = "Сохранить зашифрованный контейнер", Filter = "Контейнер PocketVault|*.pvlt", DefaultExt = ".pvlt", AddExtension = true, FileName = VaultFiles.SuggestContainerName(), InitialDirectory = Path.GetDirectoryName(input), OverwritePrompt = false };
            return save.ShowDialog(owner) == true ? save.FileName : null;
        }
        var folder = new OpenFolderDialog { Title = "Папка восстановления", InitialDirectory = Path.GetDirectoryName(input), Multiselect = false };
        return folder.ShowDialog(owner) == true ? folder.FolderName : null;
    }

    public bool ConfirmDeletion(Window owner)
    {
        var (window, panel) = CreateDialog(owner, "Удаление исходного файла",
            "Исходник будет удалён только после проверки сохранённого контейнера.\n\nОбычное удаление без Корзины не гарантирует уничтожения следов на диске.");
        var cancel = new Button { Content = "Отмена", IsCancel = true, MinWidth = 112 };
        var proceed = new Button { Content = "Продолжить", MinWidth = 128, Margin = new Thickness(12, 0, 0, 0) };
        proceed.Click += (_, _) => window.DialogResult = true;
        AddButtons(panel, cancel, proceed);
        window.Loaded += (_, _) => cancel.Focus();
        return window.ShowDialog() == true;
    }

    internal static void ShowSettings(Window owner)
    {
        var (window, panel) = CreateDialog(owner, "Настройки · файлы .pvlt",
            "Связать .pvlt с PocketVault для текущего пользователя.\nДвойной щелчок откроет контейнер для расшифровки.\nПрава администратора не нужны.");
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(feedback);
        var register = new Button { Content = "Зарегистрировать", Margin = new Thickness(0, 0, 12, 0) };
        var unregister = new Button { Content = "Отменить регистрацию" };
        register.Click += (_, _) => Apply(true);
        unregister.Click += (_, _) => Apply(false);
        AddButtons(panel, register, unregister);
        window.ShowDialog();
        void Apply(bool add)
        {
            try
            {
                if (add) FileAssociation.Register(); else FileAssociation.Unregister();
                feedback.Text = add ? "Готово. Если Windows предложит выбор приложения, выберите PocketVault." : "Регистрация отменена.";
            }
            catch (Exception ex) { feedback.Text = MainWindow.ErrorText(ex); }
        }
    }

    private static (Window Window, StackPanel Panel) CreateDialog(Window owner, string title, string text)
    {
        var window = new Window { Title = title, Owner = owner, Width = 480, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Icon = owner.Icon,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x14, 0x14, 0x14)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xF5, 0xF5)),
            FontFamily = owner.FontFamily, FontSize = owner.FontSize };
        window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PocketVault;component/Theme.xaml", UriKind.Relative) });
        var panel = new StackPanel { Margin = new Thickness(24) };
        System.Windows.Documents.TextElement.SetForeground(panel, window.Foreground);
        panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 24) });
        window.Content = panel;
        return (window, panel);
    }

    private static void AddButtons(StackPanel panel, params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in buttons) row.Children.Add(button);
        panel.Children.Add(row);
    }
}
