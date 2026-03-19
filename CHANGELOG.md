# Changelog

All notable changes to **AsyncToolWindowSample** are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased] – 2026-03-19 (feat: project-events-options-tagger)

### Added

#### `src/Services/ProjectService.cs` *(new)*
- `GetSolutionInfo()` – snapshot của solution (path, IsDirty, ProjectCount, ActiveConfig, LastBuildInfo).
- `BuildSolution()`, `CleanSolution()` – trigger build/clean.
- `GetAllProjects()` – liệt kê tất cả projects với Assembly, TargetFw, OutputType, OutputPath.
- `GetActiveDocumentProject()` – project của active document.
- `GetReferencesOfActiveProject()` – dùng `VSLangProj.VSProject`; trả về Name, Path, Version, Type.
- `GetProjectItemsOfActiveProject()` – duyệt cây file đệ quy; có Depth, BuildAction, IsFolder.
- `GetActiveBuildConfig()` – ConfigName, Platform, IsBuildable, OutputPath, Optimize, Defines.
- **DTOs:** `SolutionInfo`, `ProjectInfo`, `ReferenceInfo`, `ProjectItemInfo`, `BuildConfigInfo`.

#### `src/Services/EventService.cs` *(new)*
- Subscribe/Unsubscribe tất cả DTE events: Build, Solution, Document, Window, Selection, Debugger.
- Giữ strong references đến event sinks (tránh GC collect).
- `EventFired` event để UI layer lắng nghe log messages.
- `IDisposable` – tự unsubscribe khi package dispose.

#### `src/Services/OptionsService.cs` + `SampleOptionsPage` *(new)*
- `SampleOptionsPage : DialogPage` – 4 settings: ServerUrl, AutoFormat, MaxLogItems, EnableSelectionLog.
- `OptionsService` – typed accessors + `OpenOptionsDialog()`.
- `MyPackage` có `[ProvideOptionPage]` attribute đăng ký page với VS.

#### `src/Tagging/TodoTagger.cs` *(new)*
- `TodoTaggerProvider` – MEF `ITaggerProvider` với ContentType="text".
- `TodoTagger` – scan buffer tìm "TODO" (case-insensitive), emit `TextMarkerTag` "HighlightedReference".
- Singleton per buffer; invalidate toàn bộ file khi buffer thay đổi.
- Dispose-safe: hủy đăng ký `buffer.Changed`.

#### `docs/tutorials/project-events-options-tagger_2026-03-19.md` *(new)*

### Changed

#### `src/ToolWindows/SampleToolWindowState.cs`
- Thêm properties: `Project`, `Events`, `Options`.

#### `src/MyPackage.cs`
- Construct và wire `ProjectService`, `EventService`, `OptionsService`.
- Thêm `[ProvideOptionPage]` attribute.
- Override `Dispose()` để gọi `Events.Dispose()`.

#### `src/ToolWindows/SampleToolWindowControl.xaml`
- Thêm 4 section mới với 12 button:
  - §5: Show Solution Info, List All Projects, Active Doc Project Info, List References, List Project Files, Active Build Config, Build Solution (7 buttons)
  - §9: Subscribe to Events, Unsubscribe from Events, Show Event Status (3 buttons)
  - §10: Show Current Options, Open Options Dialog (2 buttons)
  - §12: TODO Tagger: Active?, Count TODOs in Buffer (2 buttons)

#### `src/ToolWindows/SampleToolWindowControl.xaml.cs`
- Thêm 12 handler tương ứng cho tất cả button mới.
- Thêm helper `LogNoDoc()`.

#### `src/AsyncToolWindowSample.csproj`
- Thêm Compile entries: `ProjectService.cs`, `EventService.cs`, `OptionsService.cs`, `Tagging\TodoTagger.cs`.
- Thêm Reference: `VSLangProj` (cần cho `VSProject.References` trong §5).

#### `README.md`
- Cập nhật Features, Source map cho §5, §9, §10, §12.

---

## [Unreleased] – 2026-03-19 (fix: CS1061-document-encoding)

### Fixed
- `DocumentService.cs` – CS1061: xóa `doc.Encoding` (không tồn tại trong EnvDTE 8.0).
- `SampleToolWindowControl.xaml.cs` – xóa dòng log Encoding tương ứng.

---

## [Unreleased] – 2026-03-19 (feat: document-file-apis)

### Added
- `DocumentService.cs` với đầy đủ Document & File APIs (Section 4).
- Tutorial: `docs/tutorials/document-file-apis_2026-03-19.md`.

### Changed
- `SampleToolWindowState.cs`, `MyPackage.cs`, XAML, `csproj` – integrate DocumentService.

---

## [Unreleased] – 2026-03-19 (patch: CS0122-getservice)

### Fixed
- `SelectionService.cs` – CS0122: dùng `IServiceProvider` thay `AsyncPackage.GetService`.

---

## [Unreleased] – 2026-03-19 (patch: compiler-errors)

### Fixed
- `StatusBarService.cs` – CS0165 `frozen = 0`; CS1503 `ref ulong → ref uint`.
- `SampleToolWindowControl.xaml.cs` – `uint cookie`.

---

## [1.1] – baseline
- Async Tool Window cơ bản, AsyncPackage background load.
