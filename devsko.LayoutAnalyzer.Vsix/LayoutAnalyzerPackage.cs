using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace devsko.LayoutAnalyzer
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideToolWindow(typeof(LayoutWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.SolutionExplorer)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidPackageString)]
    public sealed class LayoutAnalyzerPackage : AsyncPackage
    {
        public IVsFontAndColorStorage FontAndColorStorage { get; private set; }
        public IVsTextManager TextManager { get; private set; }
        public TextManagerEventSink TextManagerEventSink { get; private set; }
        public HostRunner HostRunner { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            //var pane = GetOutputPane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, "");
            //var current = VsColors.GetCurrentThemedColorValues().Where(kvp =>kvp.Key.Category == LayoutControl.TreeViewCategory);
            //foreach (var group in current.GroupBy(kvp => kvp.Key.Category))
            //{
            //    pane.OutputString(group.Key.ToString() + Environment.NewLine);
            //    foreach (var kvp in group)
            //    {
            //        var resourceKey = new ThemeResourceKey(group.Key, kvp.Key.Name, kvp.Key.KeyType);
            //        var value = System.Windows.Application.Current.FindResource(resourceKey);
            //        pane.OutputString($"      {kvp.Key.Name} {kvp.Key.KeyType}: {value} ({kvp.Value:x8}){Environment.NewLine}");
            //    }
            //}

            await MyToolWindowCommand.InitializeAsync(this);
        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            return toolWindowType == typeof(LayoutWindow).GUID ? this : null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            return toolWindowType == typeof(LayoutWindow) ? LayoutWindow.Title : null;
        }

        protected async override Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            TextManager = (IVsTextManager)await GetServiceAsync(typeof(SVsTextManager));
            FontAndColorStorage = (IVsFontAndColorStorage)await GetServiceAsync(typeof(SVsFontAndColorStorage));
            TextManagerEventSink = await TextManagerEventSink.SubscribeAsync(this);

            string hostBasePath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "Host");

            HostRunner = HostRunner.GetHostRunner(hostBasePath, TargetFramework.Net, Platform.x64,
#if DEBUG
                    debug: true, waitForDebugger: false
#else
                    debug: false, waitForDebugger: false
#endif
                    );

            return this;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                TextManagerEventSink?.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
