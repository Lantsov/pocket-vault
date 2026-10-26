using PocketVault.Core;

namespace PocketVault.App;

public sealed class MainForm : Form
{
    private readonly RadioButton encrypt = new() { Text = "Зашифровать", Checked = true, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, FlatStyle = FlatStyle.Flat };
    private readonly RadioButton decrypt = new() { Text = "Расшифровать", Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, FlatStyle = FlatStyle.Flat };
    private readonly TextBox file = new() { ReadOnly = true, PlaceholderText = "Выберите или перетащите один файл" };
    private readonly TextBox password = new() { UseSystemPasswordChar = true };
    private readonly TextBox confirmation = new() { UseSystemPasswordChar = true };
    private readonly Label confirmLabel = new() { Text = "Повторите пароль", AutoSize = true };
    private readonly CheckBox showPassword = new() { Text = "Показать пароль", AutoSize = true };
    private readonly CheckBox deleteOriginal = new() { Text = "Удалить исходник после проверки контейнера", AutoSize = true };
    private readonly Label deleteWarning = new() { Text = "Обычное удаление не уничтожает следы на диске.", AutoSize = true, ForeColor = Color.FromArgb(105, 110, 120) };
    private readonly Button execute = new() { Text = "Зашифровать файл", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(40, 74, 122), ForeColor = Color.White };
    private readonly Label status = new() { Text = "Полностью локально · до 128 МиБ", AutoEllipsis = true, ForeColor = Color.FromArgb(85, 95, 110) };
    private readonly TableLayoutPanel layout = new() { Dock = DockStyle.Fill, Padding = new Padding(22, 15, 22, 14), ColumnCount = 1, RowCount = 12 };
    private bool busy;

    public MainForm(string? initialFile = null)
    {
        Text = "PocketVault";
        ClientSize = new Size(500, 420);
        MinimumSize = Size;
        MaximumSize = new Size(900, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(249, 250, 252);
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("PocketVault.AppIcon")!)
        using (var icon = new Icon(iconStream)) Icon = (Icon)icon.Clone();
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int[] heights = [38, 22, 43, 22, 39, 22, 39, 26, 26, 26, 43, 36];
        foreach (int height in heights) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        Controls.Add(layout);
        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "PocketVault", Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0) });
        var settings = new LinkLabel { Text = "Файлы .pvlt", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        heading.Controls.Add(settings);
        heading.Resize += (_, _) => settings.Location = new Point(heading.Width - settings.Width, 12);
        settings.LinkClicked += (_, _) => ShowAssociationDialog();
        layout.Controls.Add(heading);
        layout.Controls.Add(new Label { Text = "Один файл. Один пароль. Только на вашем компьютере.", AutoSize = true, ForeColor = status.ForeColor });
        var mode = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 3, 0, 6) };
        mode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        encrypt.Dock = decrypt.Dock = DockStyle.Fill;
        mode.Controls.Add(encrypt); mode.Controls.Add(decrypt);
        layout.Controls.Add(mode);
        layout.Controls.Add(new Label { Text = "Файл", AutoSize = true });
        var picker = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        file.Dock = DockStyle.Fill;
        var browse = new Button { Text = "Обзор…", Dock = DockStyle.Fill, Margin = new Padding(7, 0, 0, 5) };
        picker.Controls.Add(file); picker.Controls.Add(browse);
        browse.Click += (_, _) => Browse();
        layout.Controls.Add(picker);
        layout.Controls.Add(new Label { Text = "Пароль", AutoSize = true });
        password.Dock = DockStyle.Fill; layout.Controls.Add(password);
        var confirmRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        confirmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        confirmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        confirmRow.Controls.Add(confirmLabel); confirmRow.Controls.Add(showPassword);
        layout.Controls.Add(confirmRow);
        confirmation.Dock = DockStyle.Fill; layout.Controls.Add(confirmation);
        // Опции расположены отдельно; предупреждение показывается при выборе удаления.
        deleteOriginal.Margin = Padding.Empty;
        layout.Controls.Add(deleteOriginal);
        execute.Dock = DockStyle.Fill; execute.Margin = new Padding(0, 5, 0, 5);
        layout.Controls.Add(execute);
        status.Dock = DockStyle.Fill; status.Margin = new Padding(0, 3, 0, 0);
        layout.Controls.Add(status);
        for (int row = 0; row < layout.Controls.Count; row++)
            layout.SetCellPosition(layout.Controls[row], new TableLayoutPanelCellPosition(0, row));
        AcceptButton = execute;
        encrypt.CheckedChanged += (_, _) => UpdateMode();
        showPassword.CheckedChanged += (_, _) => password.UseSystemPasswordChar = confirmation.UseSystemPasswordChar = !showPassword.Checked;
        deleteOriginal.CheckedChanged += (_, _) => status.Text = deleteOriginal.Checked ? deleteWarning.Text : "Полностью локально · до 128 МиБ";
        execute.Click += async (_, _) => await RunOperation();
        DragEnter += (_, e) => e.Effect = !busy && e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths && File.Exists(paths[0]) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (!busy && e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths) SelectFile(paths[0]); };
        FormClosing += (_, e) => { if (busy) { e.Cancel = true; status.Text = "Дождитесь завершения операции."; } };
        if (!string.IsNullOrWhiteSpace(initialFile)) SelectFile(initialFile);
        UpdateMode();
    }

    private void UpdateMode()
    {
        confirmation.Visible = confirmLabel.Visible = deleteOriginal.Visible = encrypt.Checked;
        if (!encrypt.Checked) deleteOriginal.Checked = false;
        confirmation.Clear(); password.Clear(); showPassword.Checked = false;
        execute.Text = encrypt.Checked ? "Зашифровать файл" : "Расшифровать файл";
        foreach (var button in new[] { encrypt, decrypt })
        {
            button.BackColor = button.Checked ? Color.FromArgb(225, 235, 248) : BackColor;
            button.FlatAppearance.BorderColor = button.Checked ? Color.FromArgb(70, 106, 151) : Color.FromArgb(200, 207, 217);
        }
        status.Text = "Полностью локально · до 128 МиБ";
    }

    private void SelectFile(string path)
    {
        try
        {
            file.Text = Path.GetFullPath(path);
            if (Path.GetExtension(path).Equals(".pvlt", StringComparison.OrdinalIgnoreCase)) decrypt.Checked = true;
            else encrypt.Checked = true;
            status.Text = "Файл выбран. Введите пароль.";
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { status.Text = "Не удалось прочитать путь к файлу."; }
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Title = "Выберите файл", Filter = encrypt.Checked ? "Все файлы|*.*" : "Контейнер PocketVault|*.pvlt|Все файлы|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) SelectFile(dialog.FileName);
    }

    private async Task RunOperation()
    {
        if (busy) return;
        if (!File.Exists(file.Text)) { status.Text = "Выберите существующий файл."; return; }
        if (password.Text.Length == 0) { status.Text = "Введите непустой пароль."; password.Focus(); return; }
        bool isEncrypt = encrypt.Checked;
        if (isEncrypt && password.Text != confirmation.Text) { status.Text = "Пароли не совпадают."; confirmation.Focus(); return; }
        string input = file.Text;
        string destination;
        bool remove = isEncrypt && deleteOriginal.Checked;
        if (isEncrypt)
        {
            using var save = new SaveFileDialog { Title = "Сохранить зашифрованный контейнер", Filter = "Контейнер PocketVault|*.pvlt", DefaultExt = "pvlt", AddExtension = true, FileName = VaultFiles.SuggestContainerName(), InitialDirectory = Path.GetDirectoryName(input), OverwritePrompt = false };
            if (save.ShowDialog(this) != DialogResult.OK) return;
            destination = save.FileName;
            if (remove && MessageBox.Show(this, "Исходник будет удалён только после проверки сохранённого контейнера.\n\nОбычное удаление не гарантирует уничтожения следов на диске. Продолжить?", "Удаление исходного файла", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        }
        else
        {
            using var folder = new FolderBrowserDialog { Description = "Папка восстановления (по умолчанию рядом с контейнером)", UseDescriptionForTitle = true, SelectedPath = Path.GetDirectoryName(input)! };
            if (folder.ShowDialog(this) != DialogResult.OK) return;
            destination = folder.SelectedPath;
        }
        string secret = password.Text;
        password.Clear(); confirmation.Clear(); showPassword.Checked = false;
        busy = true; layout.Enabled = false; UseWaitCursor = true;
        status.Text = isEncrypt ? "Упаковка и шифрование…" : "Проверка и восстановление…";
        try
        {
            string result = await Task.Run(() =>
            {
                if (!isEncrypt) return VaultFiles.Decrypt(input, destination, secret);
                VaultFiles.Encrypt(input, destination, secret, remove);
                return destination;
            });
            status.Text = remove ? "Готово. Контейнер проверен, исходник удалён." : "Готово: " + Path.GetFileName(result);
            MessageBox.Show(this, "Файл сохранён:\n" + result, "PocketVault", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (remove) file.Clear();
        }
        catch (Exception ex)
        {
            status.Text = ErrorText(ex);
            MessageBox.Show(this, status.Text, "PocketVault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { secret = string.Empty; busy = false; layout.Enabled = true; UseWaitCursor = false; }
    }

    private static string ErrorText(Exception error) => error switch
    {
        VaultException => error.Message,
        FileNotFoundException or DirectoryNotFoundException => "Файл или папка больше не существуют.",
        UnauthorizedAccessException => "Нет прав доступа к файлу или папке.",
        IOException io when (io.HResult & 0xFFFF) is 112 or 39 => "Недостаточно свободного места на диске.",
        IOException => "Не удалось прочитать или сохранить файл. Проверьте доступ, свободное место и совпадение имён.",
        OutOfMemoryException => "Недостаточно оперативной памяти. Закройте другие программы или выберите меньший файл.",
        _ => "Операция не завершена. Проверьте файл и доступ к выбранной папке."
    };

    private void ShowAssociationDialog()
    {
        using var dialog = new Form { Text = "Файлы .pvlt", ClientSize = new Size(410, 168), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = Font };
        dialog.Controls.Add(new Label { Text = "Связать .pvlt с PocketVault для текущего пользователя.\nДвойной щелчок откроет файл для расшифровки.\nПрава администратора не нужны.", Location = new Point(18, 17), Size = new Size(378, 68) });
        var register = new Button { Text = "Зарегистрировать", Location = new Point(18, 103), Size = new Size(180, 34) };
        var unregister = new Button { Text = "Отменить регистрацию", Location = new Point(208, 103), Size = new Size(184, 34) };
        dialog.Controls.Add(register); dialog.Controls.Add(unregister);
        register.Click += (_, _) => ApplyAssociation(true);
        unregister.Click += (_, _) => ApplyAssociation(false);
        dialog.ShowDialog(this);
        void ApplyAssociation(bool add)
        {
            try { if (add) FileAssociation.Register(); else FileAssociation.Unregister(); MessageBox.Show(dialog, add ? "Готово. Если Windows предложит выбор приложения, выберите PocketVault." : "Регистрация отменена.", "PocketVault"); }
            catch (Exception ex) { MessageBox.Show(dialog, ErrorText(ex), "PocketVault", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
