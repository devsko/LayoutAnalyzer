using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace devsko.LayoutAnalyzer
{
    public class RunningDocumentTableEventSink : IVsRunningDocTableEvents
    {
        public event Action<uint> DocumentSaved;

        private uint _cookie;

        public static async Task<RunningDocumentTableEventSink> SubscribeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            RunningDocumentTableEventSink instance = new();
            LayoutAnalyzerPackage.Instance.RunningDocumentTable.AdviseRunningDocTableEvents(instance, out instance._cookie);

            return instance;
        }

        private RunningDocumentTableEventSink()
        { }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            LayoutAnalyzerPackage.Instance.RunningDocumentTable.UnadviseRunningDocTableEvents(_cookie);
        }

        public int OnAfterSave(uint docCookie)
        {
            DocumentSaved?.Invoke(docCookie);
            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => VSConstants.S_OK;
        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => VSConstants.S_OK;
        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
            => VSConstants.S_OK;
        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
            => VSConstants.S_OK;
        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
            => VSConstants.S_OK;
    }
}
