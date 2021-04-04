using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace devsko.LayoutAnalyzer
{
    internal static class MyToolWindowCommand
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var command = new OleMenuCommand(Execute, new CommandID(PackageGuids.guidLayoutAnalyzerPackageCmdSet, PackageIds.MyCommand));
            command.BeforeQueryStatus += BeforeQueryStatus;
            var commandService = (IMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
            commandService?.AddCommand(command);

            void Execute(object sender, EventArgs args)
            {
                package.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ToolWindowPane window = await package.ShowToolWindowAsync(
                        typeof(LayoutWindow),
                        0,
                        create: true,
                        cancellationToken: package.DisposalToken);
                });
            }

            void BeforeQueryStatus(object sender, EventArgs args)
            {

            }
        }
    }
}
