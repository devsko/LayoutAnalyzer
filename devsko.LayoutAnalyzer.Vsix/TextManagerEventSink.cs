using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;

namespace LayoutAnalyzer
{
    public sealed class TextManagerEventSink : IVsTextManagerEvents, IDisposable
    {
        public event EventHandler ColorsChanged;

        private IConnectionPoint _connectionPoint;
        private uint _cookie;

        public static async Task<TextManagerEventSink> SubscribeAsync(LayoutAnalyzerPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var eventGuid = typeof(IVsTextManagerEvents).GUID;
            TextManagerEventSink instance = new TextManagerEventSink();
            ((IConnectionPointContainer)package.TextManager).FindConnectionPoint(ref eventGuid, out instance._connectionPoint);
            instance._connectionPoint.Advise(instance, out instance._cookie);

            return instance;
        }

        private TextManagerEventSink()
        { }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _connectionPoint?.Unadvise(_cookie);
            _connectionPoint = null;
        }

        public void OnRegisterMarkerType(int iMarkerType)
        { }
        public void OnRegisterView(IVsTextView pView)
        { }
        public void OnUnregisterView(IVsTextView pView)
        { }
        public void OnUserPreferencesChanged(VIEWPREFERENCES[] pViewPrefs, FRAMEPREFERENCES[] pFramePrefs, LANGPREFERENCES[] pLangPrefs, FONTCOLORPREFERENCES[] pColorPrefs)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (pColorPrefs is not null && pColorPrefs.Length > 0)
            {
                ColorsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
