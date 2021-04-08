using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServices;

namespace devsko.LayoutAnalyzer
{
    public class LayoutAnalyzer
    {
        [Import]
        public VisualStudioWorkspace Workspace { get; set; }

        public LayoutAnalyzer()
        {

        }
    }
}
