using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;

namespace LayoutAnalyzer
{
    partial class MyToolWindowControl
    {
        private static readonly Guid MefItemsCategory = new Guid("75a05685-00a8-4ded-bae5-e7a50bfa929a");
        private static readonly Guid TreeViewCategory = new Guid("92ecf08e-8b13-4cf4-99e9-ae2692382185");

        public static readonly object ForegroundKey = VsBrushes.WindowTextKey;
        public static readonly object ForegroundColorKey = VsColors.WindowTextKey;

        public static readonly ThemeResourceKey StructColorKey = new(MefItemsCategory, "", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey ClassColorKey = new(MefItemsCategory, "", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey InterfaceColorKey = new(MefItemsCategory, "", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey KeywordColorKey = new(MefItemsCategory, "", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey OperatorColorKey = new(MefItemsCategory, "operator", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey PunctuationColorKey = new(MefItemsCategory, "punctuation", ThemeResourceKeyType.ForegroundColor);

        public static readonly object TreeViewItemInactiveSelectedBackgroundKey = new ThemeResourceKey(TreeViewCategory, "SelectedItemInactive", ThemeResourceKeyType.BackgroundBrush);
    }
}
