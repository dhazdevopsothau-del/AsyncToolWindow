# Changelog — Bổ sung vào CHANGELOG.md

## [Unreleased] – 2026-03-19 (feat: configuration-editor)

### Added

#### `src/Services/ConfigurationService.cs` *(new)*
- Key constants: `KeyServerUrl`, `KeyMaxResults`, `KeyTimeoutSeconds`, `KeyEnableLogging`,
  `KeyOutputFormat`, `KeyRefreshInterval`, `KeyApiKey`, `KeyDebugMode`.
- Dictionary `Defaults` với 8 tham số mặc định.
- `InitializeAsync()` — tạo thư mục `%AppData%\AsyncToolWindowSample\`, load file XML.
- Typed getters: `GetString()`, `GetInt()`, `GetBool()`, `GetAll()`.
- Typed properties: `ServerUrl`, `MaxResults`, `TimeoutSeconds`, `EnableLogging`,
  `OutputFormat`, `RefreshInterval`, `ApiKey`, `DebugMode`.
- `Set(key, string/int/bool)` — set + log + fire `ConfigChanged` + auto-save.
- `ResetToDefaults(log)` — reset tất cả về mặc định + lưu file.
- `SaveToFile()` — ghi XML với attribute `saved` timestamp.
- `LoadFromFile()` — đọc XML, override defaults.
- `DumpToOutput()` — log toàn bộ ra Output Window (ApiKey được mask).
- `event Action<string, string> ConfigChanged` — notify khi có thay đổi.

#### `src/ToolWindows/ConfigEditorControl.xaml` *(new)*
- Form WPF với 3 sections: Connection, Behavior, Logging & Debug.
- Widgets: `TextBox` (ServerUrl, Timeout, MaxResults, RefreshInterval),
  `PasswordBox` (ApiKey), `ComboBox` (OutputFormat: JSON/XML/CSV/Plain Text),
  `CheckBox` (EnableLogging, DebugMode).
- Validation message border (màu vàng cam) + Success message border (màu xanh lá).
- Action buttons: Lưu cấu hình, Reset mặc định, Tải lại.
- Section "Xem & Test": Log tất cả config, Xem file XML, Test Get.
- Section "Quick Set": demo gọi `ConfigurationService.Set()` từ form.
- Status bar hiện path file config.

#### `src/ToolWindows/ConfigEditorControl.xaml.cs` *(new)*
- `PopulateForm()` — fill widgets từ `ConfigurationService`.
- `ValidateForm()` — kiểm tra URL format, int ranges trước khi lưu.
- `BtnSave_Click` — validate + gọi `Config.Set()` cho từng key + log.
- `BtnReset_Click` — confirm dialog + `Config.ResetToDefaults()` + repopulate.
- `BtnReload_Click` — reload form từ service.
- `BtnDump_Click` → `Config.DumpToOutput()`.
- `BtnShowFile_Click` → đọc và log nội dung file XML.
- `BtnTestGet_Click` → log tất cả typed properties (ApiKey masked).
- `BtnQuickSet_Click` → demo `Config.Set(key, value)` realtime.

#### `src/ToolWindows/ConfigEditorWindow.cs` *(new)*
- `ConfigEditorState` — DTO mang `ConfigurationService`, `OutputWindowService`, `StatusBarService`.
- `ConfigEditorWindow : ToolWindowPane` — GUID `c2d7f8a1-3e54-4b78-9d01-e2f6a8b3c094`,
  icon `KnownMonikers.Settings`, content = `ConfigEditorControl`.

#### `src/Commands/ShowConfigEditor.cs` *(new)*
- Đăng ký `MenuCommand` với `CmdIdConfigEditor (0x0300)`.
- `Execute()` → `package.ShowToolWindowAsync(typeof(ConfigEditorWindow), ...)`.

#### `docs/tutorials/configuration-editor_2026-03-19.md` *(new)*

### Changed

#### `src/PackageGuids.cs`
- Thêm `CmdIdConfigEditor = 0x0300` vào `PackageIds`.

#### `src/VSCommandTable.vsct`
- Thêm Button `CmdIdConfigEditor` dưới `IDG_VS_WNDO_OTRWNDWS1` (View > Other Windows).
- Thêm `IDSymbol name="CmdIdConfigEditor" value="0x0300"`.

#### `src/MyPackage.cs`
- Thêm property `public ConfigurationService Config`.
- Construct `Config = new ConfigurationService(this, OutputWindow)`.
- `await Config.InitializeAsync()` trong `InitializeAsync`.
- Đăng ký `[ProvideToolWindow(typeof(ConfigEditorWindow), ...)]`.
- Override `GetAsyncToolWindowFactory` + `GetToolWindowTitle` cho `ConfigEditorWindow`.
- `InitializeToolWindowAsync` trả về `ConfigEditorState` khi type = `ConfigEditorWindow`.
- `await ShowConfigEditor.InitializeAsync(this)`.

#### `src/ToolWindows/SampleToolWindowState.cs`
- Thêm `public ConfigurationService Config`.

#### `src/ToolWindows/SampleToolWindowControl.xaml`
- Thêm section "§Config Configuration Editor" với 4 button.

#### `src/ToolWindows/SampleToolWindowControl.xaml.cs`
- Thêm `private ConfigurationService Config => _state.Config;`.
- Thêm 4 handler: `Button_OpenConfigEditor_Click`, `Button_ConfigDump_Click`,
  `Button_ConfigTestGet_Click`, `Button_ConfigReset_Click`.

#### `src/AsyncToolWindowSample.csproj`
- Thêm Compile: `ConfigurationService.cs`, `ShowConfigEditor.cs`,
  `ConfigEditorWindow.cs`, `ConfigEditorControl.xaml.cs`.
- Thêm Page: `ConfigEditorControl.xaml`.

#### `README.md`
- Thêm section "Configuration Editor" vào Features table và Source map.
