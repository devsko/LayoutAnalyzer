using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace LayoutAnalyzer
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideToolWindow(typeof(MyToolWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.SolutionExplorer)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid("512c11db-6dde-43c1-9a10-d8aa821444e2")]
    public sealed class LayoutAnalyzerPackage : AsyncPackage
    {
        private TextManagerEventSink _textManagerEventSink;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await MyToolWindowCommand.InitializeAsync(this);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _textManagerEventSink.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            return toolWindowType == typeof(MyToolWindow).GUID ? this : null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            return toolWindowType == typeof(MyToolWindow) ? MyToolWindow.Title : null;
        }

        protected async override Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _textManagerEventSink = await TextManagerEventSink.SubscribeAsync(this);

            var fontAndColorStorage = (IVsFontAndColorStorage)await GetServiceAsync(typeof(SVsFontAndColorStorage));

            IVsOutputWindowPane outputWindowPane = default;
            //var outputWindow = (IVsOutputWindow)await GetServiceAsync(typeof(SVsOutputWindow));
            //if (!(outputWindow is null))
            //{
            //    Guid paneGuid = new Guid("097750EA-819D-4C0D-8918-0D6A77385E54");
            //    outputWindow.CreatePane(ref paneGuid, "Resources", 1, 0);
            //    outputWindow.GetPane(ref paneGuid, out outputWindowPane);
            //}

            return (outputWindowPane, fontAndColorStorage);
        }
    }
}
