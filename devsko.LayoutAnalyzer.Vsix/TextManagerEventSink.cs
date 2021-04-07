using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace devsko.LayoutAnalyzer
{
    public sealed class TextManagerEventSink : IVsTextManagerEvents, IDisposable
    {
        public event Action ColorsChanged;

        private uint _cookie;

        public static async Task<TextManagerEventSink> SubscribeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            TextManagerEventSink instance = new();
            GetConnectionPoint().Advise(instance, out instance._cookie);

            return instance;
        }

        private TextManagerEventSink()
        { }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            GetConnectionPoint().Unadvise(_cookie);
        }

        private static IConnectionPoint GetConnectionPoint()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var eventGuid = typeof(IVsTextManagerEvents).GUID;
            IConnectionPoint connectionPoint;
            ((IConnectionPointContainer)LayoutAnalyzerPackage.Instance.TextManager).FindConnectionPoint(ref eventGuid, out connectionPoint);

            return connectionPoint;
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
                DelayEvent(ColorsChanged);
            }
        }

        private CancellationTokenSource _tokenSource;

        private void DelayEvent(Action action)
        {
            _tokenSource?.CancelAfter(TimeSpan.FromMilliseconds(500));
            if (_tokenSource is null)
            {
                _tokenSource = new CancellationTokenSource(500);
                _tokenSource.Token.Register(() =>
                {
                    _tokenSource = null;
                    action?.Invoke();
                }, useSynchronizationContext: true);
            }
        }
    }
}
