# Hướng dẫn: Configuration Editor (§Config)

**Tính năng:** Configuration Form – Thay đổi, Lưu, Tải cấu hình  
**Ngày:** 2026-03-19  
**Phiên bản:** `feat: configuration-editor`

---

## Mục tiêu

Cung cấp `ConfigurationService` và `ConfigEditorWindow` – một form WPF hoàn chỉnh để:

- **Xem** toàn bộ cấu hình hiện tại
- **Chỉnh sửa** từng tham số qua các input widget (TextBox, CheckBox, ComboBox, PasswordBox)
- **Validate** dữ liệu trước khi lưu (type-check, range-check, URL format)
- **Lưu** xuống file XML trong `%AppData%\AsyncToolWindowSample\`
- **Tải lại** từ file khi khởi động VS
- **Reset** về giá trị mặc định
- **Log** mọi thay đổi ra Output Window
- **Test** bằng typed property getters

---

## File mới / thay đổi

| File | Loại thay đổi |
|------|---------------|
| `src/Services/ConfigurationService.cs` | **Mới** – core service |
| `src/ToolWindows/ConfigEditorControl.xaml` | **Mới** – WPF form UI |
| `src/ToolWindows/ConfigEditorControl.xaml.cs` | **Mới** – code-behind |
| `src/ToolWindows/ConfigEditorWindow.cs` | **Mới** – Tool Window + State |
| `src/Commands/ShowConfigEditor.cs` | **Mới** – command mở window |
| `src/PackageGuids.cs` | Thêm `CmdIdConfigEditor = 0x0300` |
| `src/VSCommandTable.vsct` | Thêm Button `CmdIdConfigEditor` |
| `src/MyPackage.cs` | Khởi tạo `Config`, đăng ký `ConfigEditorWindow`, `ShowConfigEditor` |
| `src/ToolWindows/SampleToolWindowState.cs` | Thêm property `Config` |
| `src/ToolWindows/SampleToolWindowControl.xaml` | Thêm 4 button §Config |
| `src/ToolWindows/SampleToolWindowControl.xaml.cs` | Thêm 4 handler §Config |
| `src/AsyncToolWindowSample.csproj` | Thêm Compile + Page entries |

---

## ConfigurationService

### Các tham số được quản lý

| Key | Type | Default | Mô tả |
|-----|------|---------|-------|
| `ServerUrl` | string | `https://api.example.com/v1` | URL server backend |
| `ApiKey` | string | `""` | API key xác thực |
| `TimeoutSeconds` | int | `30` | Timeout request (1–300 giây) |
| `MaxResults` | int | `50` | Số kết quả tối đa (1–1000) |
| `RefreshInterval` | int | `5` | Auto refresh (phút, 0=tắt) |
| `OutputFormat` | string | `"JSON"` | Định dạng xuất (JSON/XML/CSV/Plain Text) |
| `EnableLogging` | bool | `true` | Bật ghi log |
| `DebugMode` | bool | `false` | Chế độ debug chi tiết |

### API chính

```csharp
// Đọc theo kiểu
string url     = Config.ServerUrl;
int    max     = Config.MaxResults;
bool   logging = Config.EnableLogging;

// Đọc generic
string raw = Config.GetString("ServerUrl", fallback: "");
int    n   = Config.GetInt("MaxResults", fallback: 50);
bool   b   = Config.GetBool("EnableLogging", fallback: true);

// Lấy tất cả
IReadOnlyDictionary<string, string> all = Config.GetAll();

// Set + tự động lưu file
Config.Set("ServerUrl", "https://new-server.com");
Config.Set("MaxResults", 100);          // overload int
Config.Set("EnableLogging", false);     // overload bool

// Reset
Config.ResetToDefaults(log: true);

// Lưu thủ công
Config.SaveToFile();

// Log ra Output
Config.DumpToOutput();

// Subscribe event
Config.ConfigChanged += (key, newValue) => { /* ... */ };
```

### Persist (XML)

File được lưu tại:
```
%AppData%\AsyncToolWindowSample\AsyncToolWindowSample.config.xml
```

Cấu trúc:
```xml
<?xml version="1.0" encoding="utf-8" standalone="yes"?>
<AsyncToolWindowSampleConfig saved="2026-03-19 10:30:00">
  <entry key="ServerUrl" value="https://api.example.com/v1" />
  <entry key="MaxResults" value="50" />
  <entry key="TimeoutSeconds" value="30" />
  <entry key="EnableLogging" value="True" />
  <entry key="OutputFormat" value="JSON" />
  <entry key="RefreshInterval" value="5" />
  <entry key="ApiKey" value="" />
  <entry key="DebugMode" value="False" />
</AsyncToolWindowSampleConfig>
```

---

## ConfigEditorWindow (Tool Window WPF)

### Mở

- **Menu:** `View › Other Windows › Config Editor`
- **SampleToolWindow:** Button "⚙ Mở Config Editor"
- **Code:** `package.ShowToolWindowAsync(typeof(ConfigEditorWindow), ...)`

### Layout form

```
┌─────────────────────────────────────────────┐
│ ⚙  Configuration Editor                     │
│ Thay đổi, lưu và tải lại cấu hình...       │
├─────────────────────────────────────────────┤
│ 🔗  Connection                               │
│   Server URL     [https://api.example.com] │
│                  Ví dụ: https://...         │
│   API Key        [●●●●●●●●●●●●]            │
│   Timeout (giây) [30      ]                 │
├─────────────────────────────────────────────┤
│ 🔧  Behavior                                 │
│   Số kết quả     [50      ]                 │
│   Refresh (phút) [5       ]                 │
│   Output Format  [JSON ▼  ]                 │
├─────────────────────────────────────────────┤
│ 📋  Logging & Debug                          │
│   Enable Logging [✓]                        │
│   Debug Mode     [ ]                        │
│   ⚠ Debug mode có thể làm chậm VS...       │
├─────────────────────────────────────────────┤
│  [💾 Lưu cấu hình] [↩ Reset] [🔄 Tải lại] │
├─────────────────────────────────────────────┤
│ 🧪  Xem & Test                               │
│  [📋 Log tất cả] [📄 Xem XML] [🔍 Test Get]│
├─────────────────────────────────────────────┤
│ ⚡  Quick Set (ví dụ)                        │
│   Key   [ServerUrl                        ]│
│   Value [https://new-server.example.com   ]│
│  [⚡ Set ngay (ConfigurationService.Set)   ]│
├─────────────────────────────────────────────┤
│ File: %AppData%\AsyncToolWindowSample\...  │
└─────────────────────────────────────────────┘
```

### Validation rules

| Field | Rule |
|-------|------|
| Server URL | Không trống, phải bắt đầu bằng `http://` hoặc `https://` |
| Timeout | Số nguyên, 1–300 |
| MaxResults | Số nguyên, 1–1000 |
| RefreshInterval | Số nguyên, 0–60 |

---

## Buttons trong SampleToolWindow

| Button | Hành động |
|--------|-----------|
| ⚙ Mở Config Editor | Mở Config Editor Tool Window |
| 📋 Log Config hiện tại | `Config.DumpToOutput()` |
| 🔍 Test Config Getters | In tất cả typed properties ra Output |
| ↩ Reset Config mặc định | `Config.ResetToDefaults(log: true)` |

---

## Ví dụ sử dụng trong code khác

```csharp
// Trong bất kỳ service nào — inject qua constructor hoặc state
public class MyFeatureService
{
    private readonly ConfigurationService _config;

    public MyFeatureService(ConfigurationService config)
    {
        _config = config;

        // Lắng nghe thay đổi realtime
        _config.ConfigChanged += (key, val) =>
        {
            if (key == ConfigurationService.KeyServerUrl)
                ReconnectToServer(val);
        };
    }

    public void DoWork()
    {
        string url     = _config.ServerUrl;
        int    timeout = _config.TimeoutSeconds;
        bool   debug   = _config.DebugMode;
        // ... sử dụng config values
    }
}
```

---

## Thread Safety

- `ConfigurationService.InitializeAsync()` — bất kỳ thread, tự switch UI thread bên trong
- `Set()`, `SaveToFile()`, `ResetToDefaults()`, `DumpToOutput()` — **phải gọi trên UI thread**
- `GetString()`, `GetInt()`, `GetBool()`, typed properties — **thread-safe** (đọc từ Dictionary)
- `ConfigChanged` event — fire trên **UI thread**
