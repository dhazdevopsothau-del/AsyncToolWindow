using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using AsyncToolWindowSample.Services;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample.ToolWindows
{
    public partial class SampleToolWindowControl : UserControl
    {
        private readonly SampleToolWindowState _state;
        private OutputWindowService OutputWindow => _state.OutputWindow;
        private StatusBarService    StatusBar    => _state.StatusBar;
        private SelectionService    Selection    => _state.Selection;
        private DocumentService     Document     => _state.Document;
        private ProjectService      Project      => _state.Project;
        private EventService        Events       => _state.Events;
        private OptionsService      Options      => _state.Options;

        public SampleToolWindowControl(SampleToolWindowState state)
        {
            _state = state;
            InitializeComponent();
        }

        // ------------------------------------------------------------------ //
        //  Original button                                                     //
        // ------------------------------------------------------------------ //

        private void Button_ShowVsLocation_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string location = _state.DTE?.FullName ?? "(DTE not available)";
            MessageBox.Show($"Visual Studio is located here:\n'{location}'",
                "VS Location", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ------------------------------------------------------------------ //
        //  Output Window                                                       //
        // ------------------------------------------------------------------ //

        private void Button_WriteOutput_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Activate();
            OutputWindow.Log($"Button clicked – VS path: {_state.DTE?.FullName ?? "N/A"}");
            OutputWindow.WriteLine("You can write arbitrary text here.");
            StatusBar.SetText("Written to Output Window.");
        }

        private void Button_ClearOutput_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Clear();
            OutputWindow.Log("Output pane cleared.");
            StatusBar.SetText("Output pane cleared.");
        }

        // ------------------------------------------------------------------ //
        //  Status Bar                                                          //
        // ------------------------------------------------------------------ //

        private void Button_SetStatus_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StatusBar.SetText($"Hello from Async Tool Window Sample – {DateTime.Now:T}");
            OutputWindow.Log("Status bar text updated.");
        }

        private void Button_Animate_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OutputWindow.Log("Starting 3-second animation…");
                await StatusBar.RunWithAnimationAsync(
                    async () => await Task.Delay(3000),
                    "Processing… please wait");
                OutputWindow.Log("Animation finished.");
            });
        }

        private void Button_Progress_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OutputWindow.Log("Starting progress bar demo…");
                uint cookie = 0;
                const uint total = 5;
                for (uint i = 1; i <= total; i++)
                {
                    StatusBar.ReportProgress(ref cookie, "Demo progress", i, total);
                    OutputWindow.Log($"  Step {i}/{total}");
                    await Task.Delay(600);
                }
                StatusBar.ClearProgress(ref cookie);
                StatusBar.SetText("Progress complete.");
                OutputWindow.Log("Progress bar demo finished.");
            });
        }

        // ================================================================== //
        //  SELECTION TIER 1 — DTE                                             //
        // ================================================================== //

        private void Button_DteCaretInfo_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var info = Selection.GetDteCaretInfo();
            if (info == null) { LogNoDoc("[DTE]"); return; }
            OutputWindow.Activate();
            OutputWindow.Log($"[DTE Caret] Line={info.Line}, Col={info.Column}");
            OutputWindow.Log($"  Anchor  : Line={info.AnchorLine}, Col={info.AnchorDisplayColumn}, AbsOffset={info.AnchorAbsOffset}");
            OutputWindow.Log($"  Active  : Line={info.ActiveLine}, AbsOffset={info.ActiveAbsOffset}");
            OutputWindow.Log($"  TopLine={info.TopLine}, BottomLine={info.BottomLine}");
            OutputWindow.Log($"  Mode={info.Mode}, IsEmpty={info.IsEmpty}");
            if (!info.IsEmpty)
                OutputWindow.Log($"  Selected: \"{Truncate(info.SelectedText, 80)}\"");
            StatusBar.SetText($"DTE Caret – Line {info.Line}, Col {info.Column}");
        }

        private void Button_DteSelectLine_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Selection.SelectCurrentLine();
            var info = Selection.GetDteCaretInfo();
            string msg = info != null
                ? $"[DTE] Selected line {info.AnchorLine}."
                : "[DTE] SelectLine called (no caret info).";
            OutputWindow.Log(msg);
            StatusBar.SetText(msg);
        }

        private void Button_DteFindTodo_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            bool found = Selection.FindText("TODO", matchCase: false);
            string msg = found
                ? "[DTE] Found 'TODO' in active document."
                : "[DTE] 'TODO' not found in active document.";
            OutputWindow.Log(msg);
            StatusBar.SetText(msg);
        }

        private void Button_DteCollapse_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Selection.CollapseSelection();
            OutputWindow.Log("[DTE] Selection collapsed.");
            StatusBar.SetText("DTE selection collapsed.");
        }

        // ================================================================== //
        //  SELECTION TIER 2 — MEF / IWpfTextView                             //
        // ================================================================== //

        private void Button_MefCaretInfo_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var info = Selection.GetMefCaretInfo();
            if (info == null)
            {
                OutputWindow.Log("[MEF] No focused editor pane.");
                StatusBar.SetText("No focused editor pane.");
                return;
            }
            OutputWindow.Activate();
            OutputWindow.Log($"[MEF Caret] Offset={info.Offset0Based} (0-based)");
            OutputWindow.Log($"  Line={info.LineNumber0Based} (0-based) → DTE Line={info.LineNumber0Based + 1}");
            OutputWindow.Log($"  Col={info.Column0Based} (0-based)");
            OutputWindow.Log($"  Buffer: {info.TotalChars} chars, {info.TotalLines} lines");
            OutputWindow.Log($"  ContentType: {info.ContentType}");
            StatusBar.SetText($"MEF Caret – Line {info.LineNumber0Based + 1}, Col {info.Column0Based}");
        }

        private void Button_MefSelectedSpans_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var spans = Selection.GetMefSelectedSpans();
            if (spans.Count == 0)
            {
                OutputWindow.Log("[MEF] No selection (or no focused editor).");
                StatusBar.SetText("No selection.");
                return;
            }
            OutputWindow.Activate();
            OutputWindow.Log($"[MEF] {spans.Count} selected span(s):");
            for (int i = 0; i < spans.Count; i++)
            {
                var s = spans[i];
                OutputWindow.Log($"  [{i}] Start={s.Start}, End={s.End}, Len={s.Length}");
                OutputWindow.Log($"       Lines {s.StartLine}–{s.EndLine} (0-based)");
                OutputWindow.Log($"       Text: \"{Truncate(s.Text, 60)}\"");
            }
            StatusBar.SetText($"MEF: {spans.Count} span(s) selected.");
        }

        private void Button_MefInsert_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            const string inserted = "/* inserted by AsyncToolWindowSample */ ";
            Selection.InsertAtCaret(inserted);
            OutputWindow.Log($"[MEF] Inserted text at caret: \"{inserted}\"");
            StatusBar.SetText("MEF: text inserted at caret.");
        }

        private void Button_MefReplace_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var spans = Selection.GetMefSelectedSpans();
            if (spans.Count == 0 || spans[0].Length == 0)
            {
                OutputWindow.Log("[MEF] ReplaceSelection: nothing selected.");
                StatusBar.SetText("MEF: select some text first.");
                return;
            }
            string original    = spans[0].Text;
            string replacement = $"/* replaced: {original} */";
            Selection.ReplaceSelection(replacement);
            OutputWindow.Log($"[MEF] Replaced \"{Truncate(original, 40)}\" → \"{Truncate(replacement, 60)}\"");
            StatusBar.SetText("MEF: selection replaced.");
        }

        private void Button_MefBufferCount_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string text = Selection.GetBufferText();
            if (text == null)
            {
                OutputWindow.Log("[MEF] No focused editor pane.");
                StatusBar.SetText("No focused editor pane.");
                return;
            }
            string msg = $"[MEF] Buffer: {text.Length} chars, {text.Split('\n').Length} lines.";
            OutputWindow.Log(msg);
            StatusBar.SetText(msg);
        }

        // ================================================================== //
        //  DOCUMENT & FILE APIs                                               //
        // ================================================================== //

        private void Button_DocInfo_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var info = Document.GetActiveDocumentInfo();
            if (info == null) { LogNoDoc("[Doc]"); return; }
            OutputWindow.Activate();
            OutputWindow.Log($"[Doc] Name     : {info.Name}");
            OutputWindow.Log($"[Doc] FullName : {info.FullName}");
            OutputWindow.Log($"[Doc] Language : {info.Language}");
            OutputWindow.Log($"[Doc] Kind     : {info.Kind}");
            OutputWindow.Log($"[Doc] Saved    : {info.Saved}   ReadOnly: {info.ReadOnly}");
            if (info.ProjectName != null)
                OutputWindow.Log($"[Doc] Project  : {info.ProjectName}  Path: {info.ProjectFilePath}");
            StatusBar.SetText($"Doc: {info.Name} | {info.Language} | Saved={info.Saved}");
        }

        private void Button_DocListAll_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var docs = Document.GetAllOpenDocuments();
            OutputWindow.Activate();
            OutputWindow.Log($"[Doc] Open documents ({docs.Count}):");
            foreach (var d in docs)
                OutputWindow.Log($"  [{(d.Saved ? "✓" : "*")}] {d.Language,-12} {d.Name}");
            StatusBar.SetText($"Doc: {docs.Count} document(s) open.");
        }

        private void Button_TextDocInfo_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var info = Document.GetTextDocumentInfo();
            if (info == null) { OutputWindow.Log("[TextDoc] Not a text document (or none active)."); return; }
            OutputWindow.Activate();
            OutputWindow.Log($"[TextDoc] Lines : {info.FirstLine}–{info.LastLine} ({info.LastLine} total)");
            OutputWindow.Log($"[TextDoc] Chars : {info.TotalChars}");
            OutputWindow.Log($"[TextDoc] Preview: \"{Truncate(info.Preview, 160)}\"");
            StatusBar.SetText($"TextDoc: {info.LastLine} lines, {info.TotalChars} chars.");
        }

        private void Button_DocReadLines_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string lines = Document.ReadLines(1, 6);
            if (lines == null) { LogNoDoc("[TextDoc]"); return; }
            OutputWindow.Activate();
            OutputWindow.Log("[TextDoc] Lines 1–5:");
            OutputWindow.Log(Truncate(lines, 400));
            StatusBar.SetText("TextDoc: lines 1–5 read.");
        }

        private void Button_DocSave_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            bool ok  = Document.SaveActiveDocument();
            string msg = ok ? "[Doc] Active document saved." : "[Doc] No active document to save.";
            OutputWindow.Log(msg); StatusBar.SetText(msg);
        }

        private void Button_DocFormat_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var info = Document.GetActiveDocumentInfo();
            if (info == null) { LogNoDoc("[Doc]"); return; }
            Document.FormatDocument();
            string msg = $"[Doc] Edit.FormatDocument executed on {info.Name}.";
            OutputWindow.Log(msg); StatusBar.SetText(msg);
        }

        private void Button_DocSaveAll_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Document.SaveAll();
            OutputWindow.Log("[Doc] File.SaveAll executed.");
            StatusBar.SetText("All documents saved.");
        }

        private void Button_DocGoToLine_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Document.GoToLine(1);
            OutputWindow.Log("[Doc] Edit.GoToLine 1 executed.");
            StatusBar.SetText("Navigated to line 1.");
        }

        // ================================================================== //
        //  §5 PROJECT & SOLUTION APIs                                         //
        // ================================================================== //

        private void Button_SolInfo_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var info = Project.GetSolutionInfo();
            if (info == null)
            {
                OutputWindow.Log("[Solution] No solution is open.");
                StatusBar.SetText("No solution open.");
                return;
            }
            OutputWindow.Activate();
            OutputWindow.Log($"[Solution] Path        : {info.FullName}");
            OutputWindow.Log($"[Solution] IsOpen      : {info.IsOpen}   IsDirty: {info.IsDirty}");
            OutputWindow.Log($"[Solution] Projects    : {info.ProjectCount}");
            OutputWindow.Log($"[Solution] ActiveConfig: {info.ActiveConfig}");
            OutputWindow.Log($"[Solution] LastBuild   : {(info.LastBuildInfo == 0 ? "SUCCESS" : info.LastBuildInfo == -1 ? "(n/a)" : $"FAILED ({info.LastBuildInfo})")}");
            StatusBar.SetText($"Solution: {info.ProjectCount} project(s) | {info.ActiveConfig}");
        }

        private void Button_ProjList_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var projects = Project.GetAllProjects();
            OutputWindow.Activate();
            OutputWindow.Log($"[Projects] {projects.Count} project(s) in solution:");
            foreach (var p in projects)
            {
                OutputWindow.Log($"  • {p.Name}");
                OutputWindow.Log($"    Assembly : {p.AssemblyName ?? "(n/a)"}");
                OutputWindow.Log($"    Framework: {p.TargetFw    ?? "(n/a)"}");
                OutputWindow.Log($"    Output   : {p.OutputType  ?? "(n/a)"} → {p.OutputPath ?? "(n/a)"}");
            }
            StatusBar.SetText($"Projects: {projects.Count} found.");
        }

        private void Button_ProjActiveDoc_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var info = Project.GetActiveDocumentProject();
            if (info == null)
            {
                OutputWindow.Log("[Project] No active document or project not found.");
                StatusBar.SetText("No project for active document.");
                return;
            }
            OutputWindow.Activate();
            OutputWindow.Log($"[Project] Name       : {info.Name}");
            OutputWindow.Log($"[Project] UniqueName : {info.UniqueName}");
            OutputWindow.Log($"[Project] FullName   : {info.FullName}");
            OutputWindow.Log($"[Project] Assembly   : {info.AssemblyName ?? "(n/a)"}");
            OutputWindow.Log($"[Project] RootNS     : {info.RootNamespace ?? "(n/a)"}");
            OutputWindow.Log($"[Project] TargetFw   : {info.TargetFw ?? "(n/a)"}");
            OutputWindow.Log($"[Project] OutputType : {info.OutputType ?? "(n/a)"}");
            StatusBar.SetText($"Project: {info.Name}");
        }

        private void Button_ProjRefs_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var refs = Project.GetReferencesOfActiveProject();
            if (refs.Count == 0)
            {
                OutputWindow.Log("[Refs] No references found (or active doc has no C#/VB project).");
                StatusBar.SetText("No references found.");
                return;
            }
            OutputWindow.Activate();
            OutputWindow.Log($"[Refs] {refs.Count} reference(s):");
            foreach (var r in refs)
                OutputWindow.Log($"  {r.Name,-40} v{r.Version}  [{r.Type}]");
            StatusBar.SetText($"References: {refs.Count} found.");
        }

        private void Button_ProjFiles_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var items = Project.GetProjectItemsOfActiveProject();
            if (items.Count == 0)
            {
                OutputWindow.Log("[Files] No items found (or no active project).");
                StatusBar.SetText("No project items found.");
                return;
            }
            OutputWindow.Activate();
            OutputWindow.Log($"[Files] {items.Count} item(s) in project:");
            int shown = 0;
            foreach (var item in items)
            {
                if (shown++ > 50) { OutputWindow.Log("  … (truncated)"); break; }
                string indent = new string(' ', item.Depth * 2);
                string tag    = item.IsFolder ? "[DIR]" : "[FILE]";
                OutputWindow.Log($"  {indent}{tag} {item.Name}  BA={item.BuildAction ?? "-"}");
            }
            StatusBar.SetText($"Project items: {items.Count} found.");
        }

        private void Button_ProjConfig_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var cfg = Project.GetActiveBuildConfig();
            if (cfg == null)
            {
                OutputWindow.Log("[Config] No active project or config not available.");
                StatusBar.SetText("No build config found.");
                return;
            }
            OutputWindow.Activate();
            OutputWindow.Log($"[Config] Configuration : {cfg.ConfigName}|{cfg.Platform}");
            OutputWindow.Log($"[Config] IsBuildable   : {cfg.IsBuildable}");
            OutputWindow.Log($"[Config] OutputPath    : {cfg.OutputPath ?? "(n/a)"}");
            OutputWindow.Log($"[Config] Optimize      : {cfg.Optimize}");
            OutputWindow.Log($"[Config] Defines       : {cfg.Defines ?? "(n/a)"}");
            StatusBar.SetText($"Build config: {cfg.ConfigName}|{cfg.Platform}");
        }

        private void Button_SolBuild_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var info = Project.GetSolutionInfo();
            if (info == null || !info.IsOpen)
            {
                OutputWindow.Log("[Build] No solution open.");
                StatusBar.SetText("No solution open.");
                return;
            }
            OutputWindow.Log("[Build] Triggering solution build (async)…");
            StatusBar.SetText("Building solution…");
            Project.BuildSolution(waitForFinish: false);
        }

        // ================================================================== //
        //  §9 DTE EVENTS                                                      //
        // ================================================================== //

        private void Button_EventSubscribe_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Events.IsSubscribed)
            {
                OutputWindow.Log("[Events] Already subscribed.");
                StatusBar.SetText("Events: already subscribed.");
                return;
            }
            Events.Subscribe();
            StatusBar.SetText("Events: subscribed.");
        }

        private void Button_EventUnsubscribe_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!Events.IsSubscribed)
            {
                OutputWindow.Log("[Events] Not currently subscribed.");
                StatusBar.SetText("Events: not subscribed.");
                return;
            }
            Events.Unsubscribe();
            StatusBar.SetText("Events: unsubscribed.");
        }

        private void Button_EventStatus_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string state  = Events.IsSubscribed ? "ACTIVE" : "INACTIVE";
            string msg    = $"[Events] Status: {state}";
            OutputWindow.Log(msg);
            StatusBar.SetText(msg);
        }

        // ================================================================== //
        //  §10 SETTINGS / OPTIONS                                             //
        // ================================================================== //

        private void Button_OptShow_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Activate();
            OutputWindow.Log("[Options] Current settings:");
            OutputWindow.Log($"  ServerURL          : {Options.ServerUrl}");
            OutputWindow.Log($"  AutoFormat         : {Options.AutoFormat}");
            OutputWindow.Log($"  MaxLogItems        : {Options.MaxLogItems}");
            OutputWindow.Log($"  EnableSelectionLog : {Options.EnableSelectionLog}");
            StatusBar.SetText("Options: displayed in Output pane.");
        }

        private void Button_OptOpen_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Log("[Options] Opening Tools › Options dialog…");
            Options.OpenOptionsDialog();
        }

        // ================================================================== //
        //  §12 TEXT ADORNMENT / TAGGER                                        //
        // ================================================================== //

        private void Button_TaggerStatus_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // The TodoTagger is a MEF component — it is active as long as there is
            // a text buffer open. We verify by checking whether MEF editor is available.
            var view = Selection.GetActiveWpfTextView();
            if (view == null)
            {
                OutputWindow.Log("[Tagger] No active editor pane — TodoTagger not active.");
                StatusBar.SetText("Tagger: no active editor.");
                return;
            }
            OutputWindow.Log("[Tagger] TodoTagger is ACTIVE on the current buffer.");
            OutputWindow.Log($"  ContentType : {view.TextBuffer.ContentType.TypeName}");
            OutputWindow.Log($"  Buffer size : {view.TextBuffer.CurrentSnapshot.Length} chars");
            OutputWindow.Log("  Every 'TODO' token in this file is highlighted automatically.");
            StatusBar.SetText("Tagger: active on current buffer.");
        }

        private void Button_TaggerCount_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var view = Selection.GetActiveWpfTextView();
            if (view == null)
            {
                OutputWindow.Log("[Tagger] No active editor pane.");
                StatusBar.SetText("Tagger: no active editor.");
                return;
            }

            string text  = view.TextBuffer.CurrentSnapshot.GetText();
            int    count = 0;
            int    idx   = 0;
            const string keyword = "TODO";
            while ((idx = text.IndexOf(keyword, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                idx += keyword.Length;
            }

            string msg = $"[Tagger] Found {count} TODO token(s) in current buffer.";
            OutputWindow.Log(msg);
            StatusBar.SetText(msg);
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                             //
        // ------------------------------------------------------------------ //

        private void LogNoDoc(string prefix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string msg = $"{prefix} No active document.";
            OutputWindow.Log(msg);
            StatusBar.SetText(msg);
        }

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) + "…" : s ?? "";
    }
}
