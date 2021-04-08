﻿using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
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
    [Export]
    public sealed class LayoutAnalyzerPackage : AsyncPackage
    {
        public static LayoutAnalyzerPackage Instance { get; private set; }

        public LayoutAnalyzer Analyzer { get; private set; }
        public IVsFontAndColorStorage FontAndColorStorage { get; private set; }
        public IVsSolution Solution { get; private set; }
        public IVsRunningDocumentTable RunningDocumentTable { get; private set; }
        public RunningDocumentTableEventSink RunningDocumentTableEventSink { get; private set; }
        public SolutionEventSink SolutionEventSink { get; private set; }
        public IVsTextManager TextManager { get; private set; }
        public TextManagerEventSink TextManagerEventSink { get; private set; }
        public HostRunner HostRunner { get; private set; }

        private OutputWindowTextWriter _outWriter;

        public LayoutAnalyzerPackage()
        {
            Instance = this;
        }

        public async Task<OutputWindowTextWriter> GetOutAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            return _outWriter ??= new OutputWindowTextWriter(GetOutputPane(new Guid("A19B6446-F4A7-4A70-86F3-93A03B38F335"), "Layout Analyzer"));
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Analyzer = new LayoutAnalyzer();
            var componenModel = (IComponentModel)await GetServiceAsync(typeof(SComponentModel));
            Assumes.Present(componenModel);
            componenModel.DefaultCompositionService.SatisfyImportsOnce(Analyzer);

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

            FontAndColorStorage = (IVsFontAndColorStorage)await GetServiceAsync(typeof(SVsFontAndColorStorage));
            Solution = (IVsSolution)await GetServiceAsync(typeof(SVsSolution));
            SolutionEventSink = await SolutionEventSink.SubscribeAsync();
            RunningDocumentTable = (IVsRunningDocumentTable)await GetServiceAsync(typeof(SVsRunningDocumentTable));
            RunningDocumentTableEventSink = await RunningDocumentTableEventSink.SubscribeAsync();
            TextManager = (IVsTextManager)await GetServiceAsync(typeof(SVsTextManager));
            TextManagerEventSink = await TextManagerEventSink.SubscribeAsync();

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

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            HostRunner.MessageReceived +=
                async message => await (await GetOutAsync()).WriteLineAsync($"HOST ({HostRunner.Id}): " + message);
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

            return this;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                SolutionEventSink?.Dispose();
                SolutionEventSink = null;
                TextManagerEventSink?.Dispose();
                TextManagerEventSink = null;
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
