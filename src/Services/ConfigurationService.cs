using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample.Services
{
    /// <summary>
    /// Quản lý cấu hình (configuration) của extension theo dạng key-value có kiểu.
    /// Lưu trữ bằng XML trong AppData, log thay đổi ra Output Window.
    ///
    /// Tính năng:
    ///   - Lưu/đọc file XML (persist qua các lần restart VS)
    ///   - Reset về giá trị mặc định
    ///   - Validate kiểu dữ liệu khi set
    ///   - Log mọi thao tác ra OutputWindowService
    ///   - Sự kiện ConfigChanged để notify các thành phần khác
    ///
    /// All public methods must be called on the UI thread,
    /// ngoại trừ các property getter (thread-safe).
    /// </summary>
    public sealed class ConfigurationService
    {
        // ------------------------------------------------------------------ //
        //  Hằng số                                                             //
        // ------------------------------------------------------------------ //

        private const string ConfigFileName = "AsyncToolWindowSample.config.xml";
        private const string RootElement    = "AsyncToolWindowSampleConfig";

        // Key constants — dùng bên ngoài để tránh magic strings
        public const string KeyServerUrl        = "ServerUrl";
        public const string KeyMaxResults       = "MaxResults";
        public const string KeyTimeoutSeconds   = "TimeoutSeconds";
        public const string KeyEnableLogging    = "EnableLogging";
        public const string KeyOutputFormat     = "OutputFormat";
        public const string KeyRefreshInterval  = "RefreshInterval";
        public const string KeyApiKey           = "ApiKey";
        public const string KeyDebugMode        = "DebugMode";

        // ------------------------------------------------------------------ //
        //  Giá trị mặc định                                                   //
        // ------------------------------------------------------------------ //

        private static readonly Dictionary<string, object> Defaults =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [KeyServerUrl]       = "https://api.example.com/v1",
            [KeyMaxResults]      = 50,
            [KeyTimeoutSeconds]  = 30,
            [KeyEnableLogging]   = true,
            [KeyOutputFormat]    = "JSON",
            [KeyRefreshInterval] = 5,
            [KeyApiKey]          = "",
            [KeyDebugMode]       = false,
        };

        // ------------------------------------------------------------------ //
        //  Fields                                                              //
        // ------------------------------------------------------------------ //

        private readonly AsyncPackage        _package;
        private readonly IServiceProvider    _serviceProvider;
        private readonly OutputWindowService _outputWindow;

        // In-memory store (key → value as string)
        private readonly Dictionary<string, string> _store =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private string _configFilePath;
        private bool   _loaded;

        // ------------------------------------------------------------------ //
        //  Events                                                              //
        // ------------------------------------------------------------------ //

        /// <summary>Fired khi bất kỳ config key nào thay đổi.</summary>
        public event Action<string, string> ConfigChanged;   // (key, newValue)

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public ConfigurationService(AsyncPackage package, OutputWindowService outputWindow)
        {
            _package         = package      ?? throw new ArgumentNullException(nameof(package));
            _outputWindow    = outputWindow  ?? throw new ArgumentNullException(nameof(outputWindow));
            _serviceProvider = package;
        }

        // ================================================================== //
        //  Initialization                                                      //
        // ================================================================== //

        /// <summary>
        /// Xác định đường dẫn file config và load từ đĩa.
        /// Safe to call from background thread.
        /// </summary>
        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Đặt file trong %AppData%\AsyncToolWindowSample\
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder  = Path.Combine(appData, "AsyncToolWindowSample");
            Directory.CreateDirectory(folder);
            _configFilePath = Path.Combine(folder, ConfigFileName);

            // Load defaults trước, rồi override bằng giá trị từ file
            ResetToDefaults(log: false);
            LoadFromFile();

            _loaded = true;
            _outputWindow.Log($"[Config] Initialized. File: {_configFilePath}");
        }

        // ================================================================== //
        //  Typed Get                                                           //
        // ================================================================== //

        /// <summary>Lấy giá trị dạng string.</summary>
        public string GetString(string key, string fallback = "")
        {
            if (_store.TryGetValue(key, out string val)) return val;
            return fallback;
        }

        /// <summary>Lấy giá trị dạng int. Trả về <paramref name="fallback"/> nếu không parse được.</summary>
        public int GetInt(string key, int fallback = 0)
        {
            if (_store.TryGetValue(key, out string val) &&
                int.TryParse(val, out int result))
                return result;
            return fallback;
        }

        /// <summary>Lấy giá trị dạng bool.</summary>
        public bool GetBool(string key, bool fallback = false)
        {
            if (_store.TryGetValue(key, out string val) &&
                bool.TryParse(val, out bool result))
                return result;
            return fallback;
        }

        /// <summary>Lấy toàn bộ cấu hình hiện tại dưới dạng dictionary.</summary>
        public IReadOnlyDictionary<string, string> GetAll()
        {
            return new Dictionary<string, string>(_store, StringComparer.OrdinalIgnoreCase);
        }

        // ================================================================== //
        //  Set & Save                                                          //
        // ================================================================== //

        /// <summary>
        /// Đặt một giá trị config, log thay đổi và tự động lưu file.
        /// Gọi trên UI thread.
        /// </summary>
        public void Set(string key, string value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string old = _store.TryGetValue(key, out string v) ? v : "(not set)";
            _store[key] = value ?? string.Empty;

            _outputWindow.Log($"[Config] SET  {key}: \"{old}\" → \"{value}\"");
            ConfigChanged?.Invoke(key, value);
            SaveToFile();
        }

        /// <summary>Overload tiện lợi cho int.</summary>
        public void Set(string key, int value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Set(key, value.ToString());
        }

        /// <summary>Overload tiện lợi cho bool.</summary>
        public void Set(string key, bool value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Set(key, value.ToString());
        }

        // ================================================================== //
        //  Reset                                                               //
        // ================================================================== //

        /// <summary>
        /// Reset tất cả về giá trị mặc định và lưu file.
        /// </summary>
        public void ResetToDefaults(bool log = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _store.Clear();
            foreach (var kv in Defaults)
                _store[kv.Key] = kv.Value?.ToString() ?? string.Empty;

            if (log)
            {
                _outputWindow.Log("[Config] Reset về giá trị mặc định.");
                SaveToFile();
                foreach (var kv in _store)
                    _outputWindow.Log($"[Config]   {kv.Key} = {kv.Value}");
            }
        }

        // ================================================================== //
        //  Persist to / Load from XML file                                    //
        // ================================================================== //

        /// <summary>Lưu toàn bộ config ra file XML.</summary>
        public void SaveToFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrEmpty(_configFilePath)) return;

            try
            {
                var root = new XElement(RootElement,
                    new XAttribute("saved", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

                foreach (var kv in _store)
                    root.Add(new XElement("entry",
                        new XAttribute("key",   kv.Key),
                        new XAttribute("value", kv.Value)));

                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    root);
                doc.Save(_configFilePath);

                _outputWindow.Log($"[Config] Đã lưu → {_configFilePath}");
            }
            catch (Exception ex)
            {
                _outputWindow.Log($"[Config] LỖI khi lưu: {ex.Message}");
            }
        }

        /// <summary>Load config từ file XML, override giá trị trong _store.</summary>
        private void LoadFromFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!File.Exists(_configFilePath))
            {
                _outputWindow.Log("[Config] File chưa tồn tại – dùng giá trị mặc định.");
                return;
            }

            try
            {
                var doc  = XDocument.Load(_configFilePath);
                int count = 0;
                foreach (var el in doc.Root.Elements("entry"))
                {
                    string key = (string)el.Attribute("key");
                    string val = (string)el.Attribute("value");
                    if (!string.IsNullOrEmpty(key))
                    {
                        _store[key] = val ?? string.Empty;
                        count++;
                    }
                }
                _outputWindow.Log($"[Config] Load thành công – {count} entries từ file.");
            }
            catch (Exception ex)
            {
                _outputWindow.Log($"[Config] LỖI khi load: {ex.Message} – dùng mặc định.");
            }
        }

        // ================================================================== //
        //  Dump (for debugging)                                               //
        // ================================================================== //

        /// <summary>Log toàn bộ config hiện tại ra Output Window.</summary>
        public void DumpToOutput()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _outputWindow.Log($"[Config] ═══ Cấu hình hiện tại ({_store.Count} entries) ═══");
            foreach (var kv in _store)
            {
                // Ẩn ApiKey (chỉ hiện 4 ký tự cuối)
                string display = kv.Key.Equals(KeyApiKey, StringComparison.OrdinalIgnoreCase)
                    && kv.Value.Length > 4
                    ? "****" + kv.Value.Substring(kv.Value.Length - 4)
                    : kv.Value;
                _outputWindow.Log($"[Config]   {kv.Key,-20} = {display}");
            }
            _outputWindow.Log($"[Config]   (File: {_configFilePath})");
        }

        // ================================================================== //
        //  Convenience typed properties                                        //
        // ================================================================== //

        public string ServerUrl       => GetString(KeyServerUrl,       "https://api.example.com/v1");
        public int    MaxResults      => GetInt   (KeyMaxResults,       50);
        public int    TimeoutSeconds  => GetInt   (KeyTimeoutSeconds,   30);
        public bool   EnableLogging   => GetBool  (KeyEnableLogging,    true);
        public string OutputFormat    => GetString(KeyOutputFormat,     "JSON");
        public int    RefreshInterval => GetInt   (KeyRefreshInterval,  5);
        public string ApiKey          => GetString(KeyApiKey,           "");
        public bool   DebugMode       => GetBool  (KeyDebugMode,        false);
    }
}
