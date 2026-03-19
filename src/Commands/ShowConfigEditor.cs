using System;
using System.ComponentModel.Design;
using AsyncToolWindowSample.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample
{
    /// <summary>
    /// Command để mở Config Editor Tool Window từ menu.
    /// Đăng ký trong VSCommandTable.vsct với ID CmdIdConfigEditor (0x0300).
    /// </summary>
    internal sealed class ShowConfigEditor
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService =
                await package.GetServiceAsync(typeof(IMenuCommandService))
                as IMenuCommandService;

            if (commandService == null) return;

            var cmdId = new CommandID(PackageGuids.CommandSetGuid, PackageIds.CmdIdConfigEditor);
            var cmd   = new MenuCommand((s, e) => Execute(package), cmdId);
            commandService.AddCommand(cmd);
        }

        private static void Execute(AsyncPackage package)
        {
            package.JoinableTaskFactory.RunAsync(async () =>
            {
                await package.ShowToolWindowAsync(
                    typeof(ConfigEditorWindow),
                    0,
                    create: true,
                    cancellationToken: package.DisposalToken);
            });
        }
    }
}
