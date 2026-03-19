# Async Tool Window Sample

**Applies to Visual Studio 2017 Update 6 (v15.6) and newer**

This sample shows how to build a VS 2017 extension using the `AsyncPackage` pattern.
It covers Output Window, Status Bar, Selection / Caret APIs, Document & File APIs,
**Project & Solution APIs**, **DTE Events**, **Settings / Options**, and **Text Adornment (Tagger)**.

Clone the repo and open in Visual Studio 2017 to run:

```
git clone https://github.com/madskristensen/AsyncToolWindowSample
```

Press **F5** — VS launches an Experimental Instance with the extension loaded.
Open the tool window via **View › Other Windows › Sample Tool Window**.

---

## Minimum supported version

```xml
<InstallationTarget Id="Microsoft.VisualStudio.Community" Version="[15.0.27413, 16.0)" />
```

---

## Features demonstrated

### 1. AsyncPackage infrastructure
- Background loading (`AllowsBackgroundLoading = true`)
- `GetServiceAsync` (non-blocking)
- `JoinableTaskFactory.SwitchToMainThreadAsync`
- `ProvideToolWindow` + async factory (`IVsAsyncToolWindowFactory`)

### 2. Output Window (`OutputWindowService`)
| Button | What it shows |
|--------|---------------|
| Write to Output | `WriteLine` / timestamped `Log` |
| Clear Output Pane | `Clear()` |

### 3. Status Bar (`StatusBarService`)
| Button | What it shows |
|--------|---------------|
| Set Status Text | `SetText()` |
| Animate (3 s) | `StartAnimation` / `StopAnimation` |
| Show Progress Bar | 5-step progress loop |

### 4. Selection APIs — Tier 1: DTE (`SelectionService`)
| Button | What it shows |
|--------|---------------|
| Show Caret Info (DTE) | Line/Col/Anchor/Active/Mode |
| Select Current Line (DTE) | `SelectLine()` |
| Find 'TODO' (DTE) | `FindText("TODO")` |
| Collapse Selection (DTE) | `Collapse()` |

### 5. Selection APIs — Tier 2: MEF / IWpfTextView (`SelectionService`)
| Button | What it shows |
|--------|---------------|
| Show Caret Info (MEF) | Offset/Line/Col/ContentType (0-based) |
| Show Selected Spans (MEF) | All `SnapshotSpan` items |
| Insert Text at Caret (MEF) | `ITextEdit.Insert` |
| Replace Selection (MEF) | `ITextEdit.Replace` |
| Buffer Char Count (MEF) | chars + line count |

### 6. Document & File APIs (`DocumentService`)
| Button | What it shows |
|--------|---------------|
| Show Active Doc Info | Name/Language/Saved/ReadOnly/Project |
| List All Open Docs | `[✓/*] Language Name` list |
| TextDoc Info + Preview | Line count, char count, 200-char preview |
| Read Lines 1–5 | `EditPoint.GetLines` |
| Save Active Document | `doc.Save()` |
| Format Document | `Edit.FormatDocument` |
| Save All | `File.SaveAll` |
| Go To Line 1 | `Edit.GoToLine 1` |

### 7. Project & Solution APIs (`ProjectService`) — §5
| Button | What it shows |
|--------|---------------|
| Show Solution Info | Path, IsDirty, ProjectCount, ActiveConfig, LastBuild |
| List All Projects | AssemblyName, TargetFw, OutputType, OutputPath |
| Active Doc Project Info | Name, UniqueName, RootNamespace, TargetFw |
| List References | Name v{version} [Type] via `VSLangProj.VSProject` |
| List Project Files | Recursive file tree with Depth, BuildAction |
| Active Build Config | ConfigName, Platform, Optimize, Defines |
| Build Solution | `SolutionBuild.Build()` async |

### 8. DTE Events (`EventService`) — §9
| Button | What it shows |
|--------|---------------|
| Subscribe to Events | Registers Build/Solution/Document/Window/Selection/Debugger events |
| Unsubscribe from Events | Releases all sinks |
| Show Event Status | ACTIVE / INACTIVE |

All events are logged to the Output pane automatically once subscribed.

### 9. Settings / Options (`OptionsService` + `SampleOptionsPage`) — §10
| Button | What it shows |
|--------|---------------|
| Show Current Options | Prints 4 settings to Output pane |
| Open Options Dialog | `Tools › Options › Async Tool Window Sample › General` |

Settings: `ServerUrl`, `AutoFormat`, `MaxLogItems`, `EnableSelectionLog`.

### 10. Text Adornment & Tagger (`TodoTagger`) — §12
| Button | What it shows |
|--------|---------------|
| TODO Tagger: Active? | ContentType + buffer size; confirms MEF tagger is live |
| Count TODOs in Buffer | Counts "TODO" occurrences in current buffer |

The `TodoTagger` is a **MEF component** — it activates automatically for every text
file and highlights every `TODO` token using the built-in `HighlightedReference` marker style.

---

## Source map

```
src/
├── MyPackage.cs
├── VSCommandTable.vsct
├── Commands/
│   └── ShowToolWindow.cs
├── Services/
│   ├── OutputWindowService.cs
│   ├── StatusBarService.cs
│   ├── SelectionService.cs
│   ├── DocumentService.cs
│   ├── ProjectService.cs         ← §5  NEW
│   ├── EventService.cs           ← §9  NEW
│   └── OptionsService.cs         ← §10 NEW
├── Tagging/
│   └── TodoTagger.cs             ← §12 NEW
├── ToolWindows/
│   ├── SampleToolWindow.cs
│   ├── SampleToolWindowControl.xaml
│   ├── SampleToolWindowControl.xaml.cs
│   └── SampleToolWindowState.cs
└── Properties/
    └── AssemblyInfo.cs
docs/
├── VS2017-Extension-API-Reference.md
├── instructions.md
└── tutorials/
    ├── document-file-apis_2026-03-19.md
    └── project-events-options-tagger_2026-03-19.md  ← NEW
```

---

## Key concepts

### Thread safety

```csharp
await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
ThreadHelper.ThrowIfNotOnUIThread();
package.JoinableTaskFactory.RunAsync(async () => { ... });
```

### Service overview

| Service | API tier | Key dependency |
|---------|----------|----------------|
| `OutputWindowService` | COM / `IVsOutputWindow` | `SVsOutputWindow` |
| `StatusBarService` | COM / `IVsStatusbar` | `SVsStatusbar` |
| `SelectionService` | DTE + MEF | `IVsEditorAdaptersFactoryService` |
| `DocumentService` | DTE | `EnvDTE.Document` |
| `ProjectService` | DTE + `VSLangProj` | `EnvDTE.Solution`, `VSProject` |
| `EventService` | DTE Events | `EnvDTE80.Events2` |
| `OptionsService` | `DialogPage` | `AsyncPackage.GetDialogPage` |
| `TodoTagger` | MEF / `ITextBuffer` | `ITaggerProvider` export |

### DTE vs MEF selection

| Concern | DTE (Tier 1) | MEF / IWpfTextView (Tier 2) |
|---------|-------------|---------------------------|
| Offset style | 1-based | 0-based |
| Multi-caret | ✗ | ✓ |
| Edit buffer | `EditPoint` | `ITextEdit` transaction |
| Requires MEF setup | ✗ | ✓ |

---

## Further reading

- [VSCT Schema Reference](https://docs.microsoft.com/en-us/visualstudio/extensibility/vsct-xml-schema-reference)
- [AsyncPackage background load](https://docs.microsoft.com/en-us/visualstudio/extensibility/how-to-use-asyncpackage-to-load-vspackages-in-the-background)
- [IVsTextView and IWpfTextView](https://docs.microsoft.com/en-us/visualstudio/extensibility/editor-and-language-service-extensions)
- [Managed Extensibility Framework (MEF)](https://docs.microsoft.com/en-us/visualstudio/extensibility/managed-extensibility-framework-in-the-editor)
