using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;

namespace LayoutAnalyzer
{
    [Guid("ccb3a83c-8506-4de0-a783-10982481ca50")]
    public class MyToolWindow : ToolWindowPane
    {
        public const string Title = "Layout";

        public static MyToolWindow Instance { get; private set; }

        public MyToolWindow((IVsOutputWindowPane, IVsFontAndColorStorage) tuple) : base()
        {
            Instance = this;

            Caption = Title;

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            Content = new MyToolWindowControl(tuple);
        }
    }
}
