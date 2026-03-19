using System;
using System.Runtime.InteropServices;
using AsyncToolWindowSample.Services;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace AsyncToolWindowSample.ToolWindows
{
    // ====================================================================== //
    //  State object                                                            //
    // ====================================================================== //

    /// <summary>
    /// Truyền services vào <see cref="ConfigEditorControl"/>.
    /// </summary>
    public class ConfigEditorState
    {
        public ConfigurationService Config       { get; set; }
        public OutputWindowService  OutputWindow { get; set; }
        public StatusBarService     StatusBar    { get; set; }
    }

    // ====================================================================== //
    //  Tool Window                                                             //
    // ====================================================================== //

    /// <summary>
    /// VS Tool Window chứa <see cref="ConfigEditorControl"/>.
    /// Mở bằng lệnh menu "View › Other Windows › Config Editor"
    /// hoặc từ button trong SampleToolWindow.
    /// </summary>
    [Guid(WindowGuidString)]
    public class ConfigEditorWindow : ToolWindowPane
    {
        public const string WindowGuidString = "c2d7f8a1-3e54-4b78-9d01-e2f6a8b3c094";
        public const string Title            = "Config Editor";

        public ConfigEditorWindow(ConfigEditorState state) : base()
        {
            Caption            = Title;
            BitmapImageMoniker = KnownMonikers.Settings;
            Content            = new ConfigEditorControl(state);
        }
    }
}
