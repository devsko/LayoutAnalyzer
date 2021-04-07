using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace devsko.LayoutAnalyzer
{
    public class SolutionEventSink : IVsSolutionEvents
    {
        public event Action SolutionOpened;
        public event Action SolutionClosed;

        private uint _cookie;

        public static async Task<SolutionEventSink> SubscribeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            SolutionEventSink instance = new();
            LayoutAnalyzerPackage.Instance.Solution.AdviseSolutionEvents(instance, out instance._cookie);

            return instance;
        }

        private SolutionEventSink()
        { }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            LayoutAnalyzerPackage.Instance.Solution.UnadviseSolutionEvents(_cookie);
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            SolutionOpened?.Invoke();
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            SolutionClosed?.Invoke();
            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
            => VSConstants.S_OK;
        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
            => VSConstants.S_OK;
        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
            => VSConstants.S_OK;
        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
            => VSConstants.S_OK;
        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
            => VSConstants.S_OK;
        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
            => VSConstants.S_OK;
        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
            => VSConstants.S_OK;
        public int OnBeforeCloseSolution(object pUnkReserved)
            => VSConstants.S_OK;
    }
}
