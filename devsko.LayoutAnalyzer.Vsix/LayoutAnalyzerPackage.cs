using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace devsko.LayoutAnalyzer
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.PackageString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(LayoutWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.SolutionExplorer)]
    public sealed class LayoutAnalyzerPackage : AsyncPackage
    {
        public static LayoutAnalyzerPackage Instance { get; private set; }

        public VisualStudioWorkspace Workspace { get; private set; }
        public IVsFontAndColorStorage FontAndColorStorage { get; private set; }
        public IVsSolution Solution { get; private set; }
        public IVsRunningDocumentTable RunningDocumentTable { get; private set; }
        public RunningDocumentTableEventSink RunningDocumentTableEventSink { get; private set; }
        public SolutionEventSink SolutionEventSink { get; private set; }
        public IVsTextManager TextManager { get; private set; }
        public TextManagerEventSink TextManagerEventSink { get; private set; }
        public HostRunner HostRunner { get; private set; }

        public AsyncLazy<LayoutWindow> LayoutWindow { get; private set; }
        public OutputWindowTextWriter OutWriter { get; private set; }

        public LayoutAnalyzerPackage()
        {
            Instance = this;

            LayoutWindow = new AsyncLazy<LayoutWindow>(
                async () => (LayoutWindow)await FindToolWindowAsync(typeof(LayoutWindow), 0, create: true, DisposalToken),
                JoinableTaskFactory);
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            var componenModel = (IComponentModel)await GetServiceAsync(typeof(SComponentModel)).ConfigureAwait(false);
            Assumes.Present(componenModel);
            Workspace = componenModel.GetService<VisualStudioWorkspace>();

            string hostBasePath = Path.Combine(
    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
    "Host");

            HostRunner = HostRunner.GetHostRunner(hostBasePath, TargetFramework.NetCore, Platform.x64,
#if DEBUG
                    debug: true, waitForDebugger: false
#else
                    debug: false, waitForDebugger: false
#endif
                    );

            HostRunner.MessageReceived +=
                message => JoinableTaskFactory.Run(
                    async () => await OutWriter.WriteLineAsync($"HOST ({HostRunner.Id}): " + message));

            var mcs = (OleMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(false);
            Assumes.Present(mcs);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            OutWriter = new OutputWindowTextWriter(GetOutputPane(PackageGuids.OutputWindowPane, "Layout Analyzer"));

            mcs.AddCommand(new MenuCommand(
                ShowLayoutWindow,
                new CommandID(PackageGuids.CommandSet, PackageIds.LayoutWindowCommand)));

            FontAndColorStorage = (IVsFontAndColorStorage)await GetServiceAsync(typeof(SVsFontAndColorStorage));
            Solution = (IVsSolution)await GetServiceAsync(typeof(SVsSolution));
            SolutionEventSink = await SolutionEventSink.SubscribeAsync();
            RunningDocumentTable = (IVsRunningDocumentTable)await GetServiceAsync(typeof(SVsRunningDocumentTable));
            RunningDocumentTableEventSink = await RunningDocumentTableEventSink.SubscribeAsync();
            TextManager = (IVsTextManager)await GetServiceAsync(typeof(SVsTextManager));
            TextManagerEventSink = await TextManagerEventSink.SubscribeAsync();
        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            return toolWindowType == typeof(LayoutWindow).GUID ? this : null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            return toolWindowType == typeof(LayoutWindow) ? devsko.LayoutAnalyzer.LayoutWindow.Title : null;
        }

        protected override Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
            => Task.FromResult<object>(this);

        protected override void Dispose(bool disposing)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (disposing)
                {
                    SolutionEventSink?.Dispose();
                    SolutionEventSink = null;
                    TextManagerEventSink?.Dispose();
                    TextManagerEventSink = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void ShowLayoutWindow(object sender, EventArgs args)
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

                ErrorHandler.ThrowOnFailure(((IVsWindowFrame)(await LayoutWindow.GetValueAsync()).Frame).Show());
            });
        }
    }
}
