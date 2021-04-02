using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;

namespace LayoutAnalyzer
{
    [Guid("ccb3a83c-8506-4de0-a783-10982481ca50")]
    public class LayoutWindow : ToolWindowPane
    {
        public const string Title = "Layout";

        public LayoutWindow(LayoutAnalyzerPackage package)
        {
            Caption = Title;
            Content = new LayoutControl(package);
        }
    }
}
