using System;
using System.ComponentModel;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample.Services
{
    // ====================================================================== //
    //  Options Page definition                                                //
    // ====================================================================== //

    /// <summary>
    /// Appears under Tools › Options › Async Tool Window Sample › General.
    /// Properties are automatically persisted to the VS registry by DialogPage.
    /// </summary>
    public class SampleOptionsPage : DialogPage
    {
        [Category("Connection")]
        [DisplayName("Server URL")]
        [Description("URL of the backend server used by this extension.")]
        public string ServerUrl { get; set; } = "https://api.example.com";

        [Category("Behavior")]
        [DisplayName("Auto Format on Save")]
        [Description("Automatically format the active document when it is saved.")]
        public bool AutoFormat { get; set; } = false;

        [Category("Behavior")]
        [DisplayName("Max Log Items")]
        [Description("Maximum number of items shown in the Output pane log.")]
        public int MaxLogItems { get; set; } = 200;

        [Category("Behavior")]
        [DisplayName("Enable Selection Logging")]
        [Description("Log every caret/selection change event to the Output pane. " +
                     "Can be very noisy – disable for normal use.")]
        public bool EnableSelectionLog { get; set; } = false;
    }

    // ====================================================================== //
    //  Service wrapper                                                        //
    // ====================================================================== //

    /// <summary>
    /// Thin wrapper around <see cref="SampleOptionsPage"/>.
    /// Provides typed access to settings and a helper to open the Options dialog.
    /// All public methods must be called on the UI thread.
    /// </summary>
    public sealed class OptionsService
    {
        private readonly AsyncPackage _package;
        private readonly IServiceProvider _serviceProvider;

        public OptionsService(AsyncPackage package)
        {
            _package         = package ?? throw new ArgumentNullException(nameof(package));
            _serviceProvider = package;
        }

        // ------------------------------------------------------------------ //
        //  Typed accessors                                                     //
        // ------------------------------------------------------------------ //

        /// <summary>Returns the current options page instance (lazy-loaded by VS).</summary>
        public SampleOptionsPage GetPage()
            => (SampleOptionsPage)_package.GetDialogPage(typeof(SampleOptionsPage));

        public string ServerUrl          => GetPage().ServerUrl;
        public bool   AutoFormat         => GetPage().AutoFormat;
        public int    MaxLogItems        => GetPage().MaxLogItems;
        public bool   EnableSelectionLog => GetPage().EnableSelectionLog;

        // ------------------------------------------------------------------ //
        //  UI helpers                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens the Tools › Options dialog at the extension's settings page.
        /// </summary>
        public void OpenOptionsDialog()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            dte?.ExecuteCommand("Tools.Options", "Async Tool Window Sample.General");
        }
    }
}
