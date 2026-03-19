# Hướng dẫn: Project, Solution, Events, Options, Tagger APIs

**Tính năng:** Sections §5, §9, §10, §12  
**Ngày:** 2026-03-19  
**Phiên bản:** `feat: project-events-options-tagger`

---

## Mục tiêu

Triển khai 4 nhóm tính năng API tiếp theo từ `VS2017-Extension-API-Reference.md`:

| Section | Service / Component | Mô tả |
|---------|---------------------|-------|
| §5 | `ProjectService` | Project & Solution APIs |
| §9 | `EventService` | DTE Events (Build, Solution, Document, Window, Selection, Debugger) |
| §10 | `OptionsService` + `SampleOptionsPage` | Settings / Options Page |
| §12 | `TodoTagger` + `TodoTaggerProvider` | Text Adornment & Tagger (MEF) |

---

## File mới / thay đổi

| File | Loại thay đổi |
|------|---------------|
| `src/Services/ProjectService.cs` | **Mới** – §5 Project & Solution APIs |
| `src/Services/EventService.cs` | **Mới** – §9 DTE Events |
| `src/Services/OptionsService.cs` | **Mới** – §10 Settings / Options Page |
| `src/Tagging/TodoTagger.cs` | **Mới** – §12 Text Adornment & Tagger |
| `src/ToolWindows/SampleToolWindowState.cs` | Thêm `Project`, `Events`, `Options` |
| `src/MyPackage.cs` | Khởi tạo 3 service mới + `ProvideOptionPage` attribute |
| `src/ToolWindows/SampleToolWindowControl.xaml` | Thêm 12 button mới |
| `src/ToolWindows/SampleToolWindowControl.xaml.cs` | Thêm 12 handler mới |
| `src/AsyncToolWindowSample.csproj` | Thêm các Compile entry + `VSLangProj` reference |

---

## §5 — ProjectService

### Khởi tạo
`ProjectService` được construct trong `MyPackage.InitializeAsync()` và không cần `InitializeAsync()` riêng vì DTE được resolve on-demand.

### API chính

```csharp
// Solution info
SolutionInfo info = Project.GetSolutionInfo();
// info.FullName, .IsOpen, .IsDirty, .ProjectCount, .ActiveConfig, .LastBuildInfo

// Tất cả projects
IReadOnlyList<ProjectInfo> projs = Project.GetAllProjects();
// proj.Name, .UniqueName, .FullName, .Kind, .AssemblyName, .TargetFw, .OutputType, .OutputPath

// Project của active document
ProjectInfo ap = Project.GetActiveDocumentProject();

// References (VSProject / C# / VB)
IReadOnlyList<ReferenceInfo> refs = Project.GetReferencesOfActiveProject();
// r.Name, .Path, .Version, .Type

// File tree của project
IReadOnlyList<ProjectItemInfo> items = Project.GetProjectItemsOfActiveProject();
// item.Name, .Path, .BuildAction, .IsFolder, .Depth

// Build config
BuildConfigInfo cfg = Project.GetActiveBuildConfig();
// cfg.ConfigName, .Platform, .IsBuildable, .OutputPath, .Optimize, .Defines

// Build
Project.BuildSolution(waitForFinish: false);
Project.CleanSolution();
```

### Buttons trong Tool Window

| Button | API | Output |
|--------|-----|--------|
| Show Solution Info | `GetSolutionInfo()` | Path, IsDirty, ProjectCount, Config |
| List All Projects | `GetAllProjects()` | Assembly, Framework, OutputType, OutputPath |
| Active Doc Project Info | `GetActiveDocumentProject()` | Name, UniqueName, TargetFw |
| List References | `GetReferencesOfActiveProject()` | Name v{version} [Type] |
| List Project Files | `GetProjectItemsOfActiveProject()` | Cây file có indent |
| Active Build Config | `GetActiveBuildConfig()` | Config, Platform, OutputPath, Optimize |
| Build Solution | `BuildSolution()` | Trigger build (async) |

---

## §9 — EventService

### Lưu ý quan trọng về Lifetime

**PHẢI** lưu event objects vào fields (không phải local variable). `EventService` đảm bảo điều này bằng cách giữ tất cả sinks ở private fields và giải phóng trong `Unsubscribe()`.

### API chính

```csharp
// Subscribe tất cả events
Events.Subscribe();

// Unsubscribe + giải phóng references
Events.Unsubscribe();

// Kiểm tra trạng thái
bool active = Events.IsSubscribed;

// Lắng nghe event log từ UI
Events.EventFired += message => { /* cập nhật UI */ };
```

### Events được subscribe

| Event group | Events |
|-------------|--------|
| BuildEvents | `OnBuildBegin`, `OnBuildDone`, `OnBuildProjConfigBegin`, `OnBuildProjConfigDone` |
| SolutionEvents | `Opened`, `BeforeClosing`, `AfterClosing`, `ProjectAdded`, `ProjectRemoved`, `ProjectRenamed` |
| DocumentEvents | `DocumentOpened`, `DocumentClosing`, `DocumentSaved` |
| WindowEvents | `WindowActivated`, `WindowCreated`, `WindowClosing` |
| SelectionEvents | `OnChange` |
| DebuggerEvents | `OnEnterBreakMode`, `OnEnterRunMode`, `OnEnterDesignMode` |

### Buttons trong Tool Window

| Button | Hành động |
|--------|-----------|
| Subscribe to Events | `Events.Subscribe()` |
| Unsubscribe from Events | `Events.Unsubscribe()` |
| Show Event Status | Hiện ACTIVE / INACTIVE |

---

## §10 — OptionsService + SampleOptionsPage

### Cấu hình đăng ký

`MyPackage.cs` có attribute:
```csharp
[ProvideOptionPage(typeof(SampleOptionsPage),
    "Async Tool Window Sample", "General",
    categoryResourceID: 0, pageNameResourceID: 0,
    supportsAutomation: true)]
```

Trang sẽ xuất hiện tại: **Tools › Options › Async Tool Window Sample › General**

### Settings có sẵn

| Property | Type | Default | Mô tả |
|----------|------|---------|-------|
| `ServerUrl` | string | `https://api.example.com` | URL server |
| `AutoFormat` | bool | `false` | Auto format khi save |
| `MaxLogItems` | int | `200` | Số lượng log items |
| `EnableSelectionLog` | bool | `false` | Log selection changes |

### Sử dụng trong code

```csharp
// Đọc settings (lazy-loaded, tự persist vào VS registry)
string url  = Options.ServerUrl;
bool   fmt  = Options.AutoFormat;
int    max  = Options.MaxLogItems;

// Mở dialog
Options.OpenOptionsDialog();

// Lấy full page object
SampleOptionsPage page = Options.GetPage();
```

### Buttons trong Tool Window

| Button | Hành động |
|--------|-----------|
| Show Current Options | In 4 settings vào Output pane |
| Open Options Dialog | `Tools.Options` command |

---

## §12 — TodoTagger (MEF)

### Kiến trúc

```
TodoTaggerProvider (MEF Export: ITaggerProvider)
    → Tạo TodoTagger cho mỗi ITextBuffer
        → Scan text tìm "TODO" (case-insensitive)
        → Emit TextMarkerTag với style "HighlightedReference"
        → Invalidate khi buffer thay đổi (ITextEdit.Apply)
```

### Đặc điểm

- **MEF-based**: Tự động active cho mọi file text khi extension load, không cần khởi tạo thủ công.
- **ContentType = "text"**: Áp dụng cho tất cả file text (C#, VB, XML, JSON, Plain Text...).
- **Singleton per buffer**: `buffer.Properties.GetOrCreateSingletonProperty` đảm bảo chỉ 1 tagger mỗi buffer.
- **Dispose-safe**: Hủy đăng ký `buffer.Changed` trong `Dispose()`.

### Buttons trong Tool Window

| Button | Hành động |
|--------|-----------|
| TODO Tagger: Active? | Kiểm tra IWpfTextView có sẵn, hiện ContentType + buffer size |
| Count TODOs in Buffer | Đếm số lần xuất hiện "TODO" trong buffer hiện tại |

---

## Thread Safety

Tất cả Service methods đều yêu cầu UI thread:

```csharp
ThreadHelper.ThrowIfNotOnUIThread();
// hoặc
await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
```

`EventService.Subscribe()` / `Unsubscribe()` phải gọi trên UI thread.  
`EventService.Dispose()` an toàn từ bất kỳ thread nào (tự switch).

---

## Dependency giữa services

```
MyPackage
├── OutputWindowService  (init trước EventService)
├── StatusBarService
├── SelectionService     (cung cấp GetActiveWpfTextView cho Tagger button)
├── DocumentService
├── ProjectService       (dùng VSLangProj.VSProject cho References)
├── EventService         (phụ thuộc OutputWindowService)
└── OptionsService
```

`TodoTagger` là MEF component — không phụ thuộc bất kỳ service nào ở trên.
