using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
