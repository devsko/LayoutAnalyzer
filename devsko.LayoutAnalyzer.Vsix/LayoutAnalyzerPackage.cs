using System;
using System.Collections.Immutable;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using IVsTextManager = Microsoft.VisualStudio.TextManager.Interop.IVsTextManager;
using SVsTextManager = Microsoft.VisualStudio.TextManager.Interop.SVsTextManager;
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
        public DTE2 DTE { get; private set; }
        public IVsFontAndColorStorage FontAndColorStorage { get; private set; }
        //public IVsSolution Solution { get; private set; }
        //public IVsRunningDocumentTable RunningDocumentTable { get; private set; }
        //public RunningDocumentTableEventSink RunningDocumentTableEventSink { get; private set; }
        //public SolutionEventSink SolutionEventSink { get; private set; }
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

            HostRunner = HostRunner.GetHostRunner(hostBasePath, TargetFramework.Net5Plus, Platform.x64,
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
            mcs.AddCommand(new MenuCommand(
                Analyze,
                new CommandID(PackageGuids.ContextMenuCommandSet, PackageIds.AnalyzeCommand)));

            DTE = (DTE2)await GetServiceAsync(typeof(SDTE));
            Assumes.Present(DTE);
            FontAndColorStorage = (IVsFontAndColorStorage)await GetServiceAsync(typeof(SVsFontAndColorStorage));
            Assumes.Present(FontAndColorStorage);
            //Solution = (IVsSolution)await GetServiceAsync(typeof(SVsSolution));
            //SolutionEventSink = await SolutionEventSink.SubscribeAsync();
            //RunningDocumentTable = (IVsRunningDocumentTable)await GetServiceAsync(typeof(SVsRunningDocumentTable));
            //RunningDocumentTableEventSink = await RunningDocumentTableEventSink.SubscribeAsync();
            TextManager = (IVsTextManager)await GetServiceAsync(typeof(SVsTextManager));
            Assumes.Present(TextManager);
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
                    //SolutionEventSink?.Dispose();
                    //SolutionEventSink = null;
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

        private void Analyze(object sender, EventArgs args)
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

                EnvDTE.Document dteDocument = DTE.ActiveDocument;
                if (dteDocument is not null)
                {
                    EnvDTE.Project dteProject = dteDocument.ProjectItem?.ContainingProject;
                    if (dteProject is not null)
                    {
                        EnvDTE.TextSelection selection = (EnvDTE.TextSelection)dteDocument.Selection;
                        if (selection is not null)
                        {
                            // TODO CRLF?
                            int position = selection.ActivePoint.AbsoluteCharOffset + selection.CurrentLine - 1;
                            string filePath = dteDocument.FullName;
                            ImmutableArray<DocumentId> documentIds = Workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath);
                            if (!documentIds.IsEmpty)
                            {
                                Document document = Workspace.CurrentSolution.GetDocument(documentIds[0]);

                                ISymbol symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position).ConfigureAwait(false);

                                //SemanticModel semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
                                //SyntaxTree syntaxTree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);

                                //GetNearestType(semanticModel, syntaxTree, offset);
                            }
                        }
                    }
                }
            });
        }

        private static void GetNearestType(SemanticModel semanticModel, SyntaxTree syntaxTree, int offset)
        {
            FileLinePositionSpan span = syntaxTree.GetLineSpan(new TextSpan(offset, 0));
        }
    }
}
