using System;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample.Services
{
    /// <summary>
    /// Wraps DTE Events APIs (Section 9).
    /// Subscribes to BuildEvents, SolutionEvents, DocumentEvents,
    /// WindowEvents, SelectionEvents and DebuggerEvents.
    ///
    /// IMPORTANT: All event objects are held as fields to prevent GC collection.
    /// Call <see cref="Subscribe"/> to start listening and <see cref="Unsubscribe"/>
    /// to stop.  All callbacks are marshalled from the UI thread.
    /// </summary>
    public sealed class EventService : IDisposable
    {
        private readonly AsyncPackage _package;
        private readonly IServiceProvider _serviceProvider;
        private readonly OutputWindowService _outputWindow;

        // ── Strong references to event sinks (MUST be fields, not locals) ──
        private BuildEvents     _buildEvents;
        private SolutionEvents  _solutionEvents;
        private DocumentEvents  _documentEvents;
        private WindowEvents    _windowEvents;
        private SelectionEvents _selectionEvents;
        private DebuggerEvents  _debuggerEvents;

        private bool _subscribed;
        private bool _disposed;

        // ── Raised for UI consumption ──────────────────────────────────────
        public event Action<string> EventFired;

        public EventService(AsyncPackage package, OutputWindowService outputWindow)
        {
            _package         = package         ?? throw new ArgumentNullException(nameof(package));
            _outputWindow    = outputWindow     ?? throw new ArgumentNullException(nameof(outputWindow));
            _serviceProvider = package;
        }

        private DTE2 GetDte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _serviceProvider.GetService(typeof(DTE)) as DTE2;
        }

        // ================================================================== //
        //  Subscribe / Unsubscribe                                            //
        // ================================================================== //

        /// <summary>
        /// Registers all event handlers.
        /// Must be called on the UI thread.
        /// </summary>
        public void Subscribe()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_subscribed) return;

            var dte = GetDte();
            if (dte == null) return;

            var events = (Events2)dte.Events;

            // ── Build ────────────────────────────────────────────────────────
            _buildEvents = events.BuildEvents;
            _buildEvents.OnBuildBegin += OnBuildBegin;
            _buildEvents.OnBuildDone  += OnBuildDone;
            _buildEvents.OnBuildProjConfigBegin += OnBuildProjConfigBegin;
            _buildEvents.OnBuildProjConfigDone  += OnBuildProjConfigDone;

            // ── Solution ──────────────────────────────────────────────────────
            _solutionEvents = events.SolutionEvents;
            _solutionEvents.Opened         += OnSolutionOpened;
            _solutionEvents.BeforeClosing  += OnSolutionBeforeClosing;
            _solutionEvents.AfterClosing   += OnSolutionAfterClosing;
            _solutionEvents.ProjectAdded   += OnProjectAdded;
            _solutionEvents.ProjectRemoved += OnProjectRemoved;
            _solutionEvents.ProjectRenamed += OnProjectRenamed;

            // ── Document ──────────────────────────────────────────────────────
            _documentEvents = events.get_DocumentEvents(null); // null = all docs
            _documentEvents.DocumentOpened  += OnDocumentOpened;
            _documentEvents.DocumentClosing += OnDocumentClosing;
            _documentEvents.DocumentSaved   += OnDocumentSaved;

            // ── Window ────────────────────────────────────────────────────────
            _windowEvents = events.get_WindowEvents(null); // null = all windows
            _windowEvents.WindowActivated += OnWindowActivated;
            _windowEvents.WindowCreated   += OnWindowCreated;
            _windowEvents.WindowClosing   += OnWindowClosing;

            // ── Selection ─────────────────────────────────────────────────────
            _selectionEvents = events.SelectionEvents;
            _selectionEvents.OnChange += OnSelectionChange;

            // ── Debugger ──────────────────────────────────────────────────────
            _debuggerEvents = events.DebuggerEvents;
            _debuggerEvents.OnEnterBreakMode  += OnEnterBreakMode;
            _debuggerEvents.OnEnterRunMode    += OnEnterRunMode;
            _debuggerEvents.OnEnterDesignMode += OnEnterDesignMode;

            _subscribed = true;
            Log("[Events] All event handlers subscribed.");
        }

        /// <summary>
        /// Unregisters all event handlers and releases event sinks.
        /// Must be called on the UI thread.
        /// </summary>
        public void Unsubscribe()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_subscribed) return;

            if (_buildEvents != null)
            {
                _buildEvents.OnBuildBegin           -= OnBuildBegin;
                _buildEvents.OnBuildDone            -= OnBuildDone;
                _buildEvents.OnBuildProjConfigBegin -= OnBuildProjConfigBegin;
                _buildEvents.OnBuildProjConfigDone  -= OnBuildProjConfigDone;
                _buildEvents = null;
            }

            if (_solutionEvents != null)
            {
                _solutionEvents.Opened         -= OnSolutionOpened;
                _solutionEvents.BeforeClosing  -= OnSolutionBeforeClosing;
                _solutionEvents.AfterClosing   -= OnSolutionAfterClosing;
                _solutionEvents.ProjectAdded   -= OnProjectAdded;
                _solutionEvents.ProjectRemoved -= OnProjectRemoved;
                _solutionEvents.ProjectRenamed -= OnProjectRenamed;
                _solutionEvents = null;
            }

            if (_documentEvents != null)
            {
                _documentEvents.DocumentOpened  -= OnDocumentOpened;
                _documentEvents.DocumentClosing -= OnDocumentClosing;
                _documentEvents.DocumentSaved   -= OnDocumentSaved;
                _documentEvents = null;
            }

            if (_windowEvents != null)
            {
                _windowEvents.WindowActivated -= OnWindowActivated;
                _windowEvents.WindowCreated   -= OnWindowCreated;
                _windowEvents.WindowClosing   -= OnWindowClosing;
                _windowEvents = null;
            }

            if (_selectionEvents != null)
            {
                _selectionEvents.OnChange -= OnSelectionChange;
                _selectionEvents = null;
            }

            if (_debuggerEvents != null)
            {
                _debuggerEvents.OnEnterBreakMode  -= OnEnterBreakMode;
                _debuggerEvents.OnEnterRunMode    -= OnEnterRunMode;
                _debuggerEvents.OnEnterDesignMode -= OnEnterDesignMode;
                _debuggerEvents = null;
            }

            _subscribed = false;
            Log("[Events] All event handlers unsubscribed.");
        }

        public bool IsSubscribed => _subscribed;

        // ================================================================== //
        //  Build handlers                                                      //
        // ================================================================== //

        private void OnBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Build] Started — scope={scope}, action={action}");
        }

        private void OnBuildDone(vsBuildScope scope, vsBuildAction action)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte      = GetDte();
            int failures = dte?.Solution?.SolutionBuild?.LastBuildInfo ?? -1;
            string result = failures == 0 ? "SUCCESS" : $"FAILED (failures={failures})";
            Log($"[Build] Done — {result}  scope={scope}");
        }

        private void OnBuildProjConfigBegin(string project, string projConfig, string platform, string solutionConfig)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Build] Project start — {project} [{projConfig}|{platform}]");
        }

        private void OnBuildProjConfigDone(string project, string projConfig, string platform, string solutionConfig, bool success)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Build] Project done  — {project}: {(success ? "OK" : "FAIL")}");
        }

        // ================================================================== //
        //  Solution handlers                                                   //
        // ================================================================== //

        private void OnSolutionOpened()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = GetDte();
            Log($"[Solution] Opened: {dte?.Solution?.FullName ?? "(unknown)"}");
        }

        private void OnSolutionBeforeClosing()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log("[Solution] Closing…");
        }

        private void OnSolutionAfterClosing()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log("[Solution] Closed.");
        }

        private void OnProjectAdded(Project p)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Solution] Project added: {p?.Name}");
        }

        private void OnProjectRemoved(Project p)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Solution] Project removed: {p?.Name}");
        }

        private void OnProjectRenamed(Project p, string oldName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Solution] Project renamed: {oldName} → {p?.Name}");
        }

        // ================================================================== //
        //  Document handlers                                                   //
        // ================================================================== //

        private void OnDocumentOpened(Document doc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Document] Opened: {doc?.Name}");
        }

        private void OnDocumentClosing(Document doc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Document] Closing: {doc?.Name} (saved={doc?.Saved})");
        }

        private void OnDocumentSaved(Document doc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Document] Saved: {doc?.FullName}");
        }

        // ================================================================== //
        //  Window handlers                                                     //
        // ================================================================== //

        private void OnWindowActivated(Window gotFocus, Window lostFocus)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Window] Activated: {gotFocus?.Caption}");
        }

        private void OnWindowCreated(Window win)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Window] Created: {win?.Caption}");
        }

        private void OnWindowClosing(Window win)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Window] Closing: {win?.Caption}");
        }

        // ================================================================== //
        //  Selection handler                                                   //
        // ================================================================== //

        private void OnSelectionChange()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = GetDte();
            var sel = dte?.ActiveDocument?.Selection as TextSelection;
            if (sel == null) return;
            // Selection fires very frequently; only raise the public event (no log spam)
            EventFired?.Invoke($"[Selection] Line={sel.CurrentLine}, Col={sel.CurrentColumn}");
        }

        // ================================================================== //
        //  Debugger handlers                                                   //
        // ================================================================== //

        private void OnEnterBreakMode(dbgEventReason reason, ref dbgExecutionAction executionAction)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Debugger] Break mode — reason={reason}");
        }

        private void OnEnterRunMode(dbgEventReason reason)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Debugger] Run mode — reason={reason}");
        }

        private void OnEnterDesignMode(dbgEventReason reason)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Log($"[Debugger] Design mode — reason={reason}");
        }

        // ================================================================== //
        //  Helpers                                                             //
        // ================================================================== //

        private void Log(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _outputWindow.Log(message);
            EventFired?.Invoke(message);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Fire-and-forget unsubscribe on UI thread
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Unsubscribe();
            });
        }
    }
}
