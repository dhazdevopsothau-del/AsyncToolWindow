using AsyncToolWindowSample.Services;

namespace AsyncToolWindowSample.ToolWindows
{
    /// <summary>
    /// State object passed from <see cref="MyPackage.InitializeToolWindowAsync"/>
    /// into <see cref="SampleToolWindow"/> constructor.
    /// Carries DTE and all registered services.
    /// </summary>
    public class SampleToolWindowState
    {
        public EnvDTE80.DTE2 DTE { get; set; }

        /// <summary>Output Window pane managed by this extension.</summary>
        public OutputWindowService OutputWindow { get; set; }

        /// <summary>Status bar wrapper.</summary>
        public StatusBarService StatusBar { get; set; }

        /// <summary>Selection / caret API wrapper (DTE + MEF tiers).</summary>
        public SelectionService Selection { get; set; }

        /// <summary>Document &amp; File API wrapper.</summary>
        public DocumentService Document { get; set; }

        /// <summary>Project &amp; Solution API wrapper (Section 5).</summary>
        public ProjectService Project { get; set; }

        /// <summary>DTE Events wrapper (Section 9).</summary>
        public EventService Events { get; set; }

        /// <summary>Settings / Options wrapper (Section 10).</summary>
        public OptionsService Options { get; set; }
    }
}
