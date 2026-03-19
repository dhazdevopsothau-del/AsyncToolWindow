using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AsyncToolWindowSample.Services;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample.ToolWindows
{
    /// <summary>
    /// Code-behind cho ConfigEditorControl.xaml.
    /// Form cho phép xem, chỉnh sửa, lưu và test ConfigurationService.
    /// </summary>
    public partial class ConfigEditorControl : UserControl
    {
        private readonly ConfigEditorState  _state;
        private ConfigurationService        Config      => _state.Config;
        private OutputWindowService         OutputWindow => _state.OutputWindow;
        private StatusBarService            StatusBar   => _state.StatusBar;

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public ConfigEditorControl(ConfigEditorState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            PopulateForm();
            // Subscribe để cập nhật form khi config thay đổi từ nơi khác
            Config.ConfigChanged += OnConfigChangedExternally;
        }

        // ================================================================== //
        //  Populate form từ ConfigurationService                              //
        // ================================================================== //

        private void PopulateForm()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            TxtServerUrl.Text        = Config.ServerUrl;
            PwdApiKey.Password       = Config.ApiKey;
            TxtTimeout.Text          = Config.TimeoutSeconds.ToString();
            TxtMaxResults.Text       = Config.MaxResults.ToString();
            TxtRefreshInterval.Text  = Config.RefreshInterval.ToString();
            ChkEnableLogging.IsChecked = Config.EnableLogging;
            ChkDebugMode.IsChecked     = Config.DebugMode;

            // ComboBox: chọn đúng item
            SelectComboItem(CmbOutputFormat, Config.OutputFormat);

            // File path
            var all = Config.GetAll();
            // Lấy path từ service qua reflection trên private field không thực tế,
            // nên ta hiển thị từ AppData
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AsyncToolWindowSample", "AsyncToolWindowSample.config.xml");
            TxtFilePath.Text = folder;

            HideMessages();
        }

        private void SelectComboItem(ComboBox cmb, string text)
        {
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (string.Equals(item.Content?.ToString(), text,
                        StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        // ================================================================== //
        //  Validate                                                            //
        // ================================================================== //

        private bool ValidateForm(out string error)
        {
            // ServerUrl
            string url = TxtServerUrl.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(url))
            { error = "Server URL không được để trống."; return false; }
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            { error = "Server URL phải bắt đầu bằng http:// hoặc https://"; return false; }

            // Timeout
            if (!int.TryParse(TxtTimeout.Text, out int timeout) || timeout < 1 || timeout > 300)
            { error = "Timeout phải là số nguyên từ 1 đến 300."; return false; }

            // MaxResults
            if (!int.TryParse(TxtMaxResults.Text, out int maxR) || maxR < 1 || maxR > 1000)
            { error = "Số kết quả tối đa phải từ 1 đến 1000."; return false; }

            // RefreshInterval
            if (!int.TryParse(TxtRefreshInterval.Text, out int refresh) || refresh < 0 || refresh > 60)
            { error = "Refresh interval phải từ 0 đến 60 phút."; return false; }

            error = null;
            return true;
        }

        // ================================================================== //
        //  Button handlers                                                     //
        // ================================================================== //

        /// <summary>Lưu cấu hình từ form vào ConfigurationService.</summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!ValidateForm(out string error))
            {
                ShowValidation(error);
                return;
            }

            OutputWindow.Activate();
            OutputWindow.Log("[Config] ── Bắt đầu lưu cấu hình từ form ──");

            Config.Set(ConfigurationService.KeyServerUrl,       TxtServerUrl.Text.Trim());
            Config.Set(ConfigurationService.KeyApiKey,          PwdApiKey.Password);
            Config.Set(ConfigurationService.KeyTimeoutSeconds,  int.Parse(TxtTimeout.Text));
            Config.Set(ConfigurationService.KeyMaxResults,      int.Parse(TxtMaxResults.Text));
            Config.Set(ConfigurationService.KeyRefreshInterval, int.Parse(TxtRefreshInterval.Text));
            Config.Set(ConfigurationService.KeyEnableLogging,   ChkEnableLogging.IsChecked == true);
            Config.Set(ConfigurationService.KeyDebugMode,       ChkDebugMode.IsChecked == true);

            string fmt = (CmbOutputFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";
            Config.Set(ConfigurationService.KeyOutputFormat, fmt);

            OutputWindow.Log("[Config] ── Lưu hoàn tất ──");

            ShowSuccess("✓ Đã lưu cấu hình thành công!");
            StatusBar.SetText("Config: Đã lưu cấu hình.");
        }

        /// <summary>Reset về mặc định rồi cập nhật form.</summary>
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var result = MessageBox.Show(
                "Reset tất cả về giá trị mặc định?\nThao tác này sẽ ghi đè file config.",
                "Xác nhận Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            OutputWindow.Activate();
            Config.ResetToDefaults(log: true);
            PopulateForm();
            ShowSuccess("✓ Đã reset về giá trị mặc định.");
            StatusBar.SetText("Config: Reset về mặc định.");
        }

        /// <summary>Tải lại giá trị từ ConfigurationService vào form (bỏ thay đổi chưa lưu).</summary>
        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            PopulateForm();
            ShowSuccess("✓ Đã tải lại từ ConfigurationService.");
            OutputWindow.Log("[Config] Form reloaded từ ConfigurationService.");
            StatusBar.SetText("Config: Reloaded.");
        }

        /// <summary>Log toàn bộ config ra Output Window.</summary>
        private void BtnDump_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Activate();
            Config.DumpToOutput();
            StatusBar.SetText("Config: Đã log ra Output.");
        }

        /// <summary>Đọc và hiển thị nội dung file XML ra Output Window.</summary>
        private void BtnShowFile_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string folder   = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AsyncToolWindowSample");
            string filePath = Path.Combine(folder, "AsyncToolWindowSample.config.xml");

            OutputWindow.Activate();
            OutputWindow.Log($"[Config] ── Nội dung file XML ──");
            OutputWindow.Log($"[Config] Path: {filePath}");

            if (!File.Exists(filePath))
            {
                OutputWindow.Log("[Config] File chưa tồn tại.");
                return;
            }

            try
            {
                string content = File.ReadAllText(filePath);
                // In từng dòng để đẹp hơn
                foreach (string line in content.Split('\n'))
                    OutputWindow.Log("[XML] " + line.TrimEnd());
            }
            catch (Exception ex)
            {
                OutputWindow.Log($"[Config] LỖI đọc file: {ex.Message}");
            }
        }

        /// <summary>Demo lấy giá trị bằng typed getters.</summary>
        private void BtnTestGet_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Activate();
            OutputWindow.Log("[Config] ── Test typed getters ──");
            OutputWindow.Log($"[Config]  Config.ServerUrl       = \"{Config.ServerUrl}\"");
            OutputWindow.Log($"[Config]  Config.MaxResults      = {Config.MaxResults}");
            OutputWindow.Log($"[Config]  Config.TimeoutSeconds  = {Config.TimeoutSeconds}");
            OutputWindow.Log($"[Config]  Config.EnableLogging   = {Config.EnableLogging}");
            OutputWindow.Log($"[Config]  Config.OutputFormat    = \"{Config.OutputFormat}\"");
            OutputWindow.Log($"[Config]  Config.RefreshInterval = {Config.RefreshInterval}");
            OutputWindow.Log($"[Config]  Config.DebugMode       = {Config.DebugMode}");
            // ApiKey — ẩn
            string apiKey = Config.ApiKey;
            string masked = apiKey.Length > 4 ? "****" + apiKey.Substring(apiKey.Length - 4) : "****";
            OutputWindow.Log($"[Config]  Config.ApiKey          = \"{masked}\" (masked)");
            OutputWindow.Log("[Config] ── Kết thúc test ──");
            StatusBar.SetText("Config: Test getters xong.");
        }

        /// <summary>Demo gọi ConfigurationService.Set() trực tiếp từ code.</summary>
        private void BtnQuickSet_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string key   = TxtQuickKey.Text?.Trim() ?? "";
            string value = TxtQuickValue.Text ?? "";

            if (string.IsNullOrWhiteSpace(key))
            {
                ShowValidation("Key không được để trống.");
                return;
            }

            OutputWindow.Activate();
            Config.Set(key, value);

            // Cập nhật lại form để phản ánh thay đổi
            PopulateForm();
            ShowSuccess($"✓ Set \"{key}\" = \"{value}\" thành công.");
            StatusBar.SetText($"Config: Set {key}.");
        }

        // ================================================================== //
        //  External config change handler                                      //
        // ================================================================== //

        private void OnConfigChangedExternally(string key, string newValue)
        {
            // Có thể refresh form nếu cần (chú ý: có thể không trên UI thread)
            // Ở đây để đơn giản, không tự refresh để tránh overwrite input của user
        }

        // ================================================================== //
        //  Message helpers                                                     //
        // ================================================================== //

        private void ShowValidation(string message)
        {
            TxtValidation.Text        = "⚠  " + message;
            ValidationBorder.Visibility = Visibility.Visible;
            SuccessBorder.Visibility    = Visibility.Collapsed;
        }

        private void ShowSuccess(string message)
        {
            TxtSuccess.Text          = message;
            SuccessBorder.Visibility  = Visibility.Visible;
            ValidationBorder.Visibility = Visibility.Collapsed;
        }

        private void HideMessages()
        {
            ValidationBorder.Visibility = Visibility.Collapsed;
            SuccessBorder.Visibility    = Visibility.Collapsed;
        }
    }
}
