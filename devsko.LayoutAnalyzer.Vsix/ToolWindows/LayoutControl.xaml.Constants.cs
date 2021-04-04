using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;

namespace devsko.LayoutAnalyzer
{
    partial class LayoutControl
    {
        private static readonly Guid MefItemsCategory = new Guid("75a05685-00a8-4ded-bae5-e7a50bfa929a");
        private static readonly Guid TreeViewCategory = new Guid("92ecf08e-8b13-4cf4-99e9-ae2692382185");

        public static readonly object ForegroundKey = VsBrushes.WindowTextKey;
        public static readonly object ForegroundColorKey = VsColors.WindowTextKey;

        public static readonly object BackgroundKey = VsBrushes.ToolWindowBackgroundKey;
        public static readonly object BackgroundColorKey = VsColors.ToolWindowBackgroundKey;

        public static readonly ThemeResourceKey IdentifierForegroundColorKey = new(MefItemsCategory, "Plain Text", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey ClassForegroundColorKey = new(MefItemsCategory, "class name", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey EnumForegroundColorKey = new(MefItemsCategory, "enum name", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey StructForegroundColorKey = new(MefItemsCategory, "struct name", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey InterfaceForegroundColorKey = new(MefItemsCategory, "interface name", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey KeywordForegroundColorKey = new(MefItemsCategory, "Keyword", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey OperatorForegroundColorKey = new(MefItemsCategory, "Operator", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey PunctuationForegroundColorKey = new(MefItemsCategory, "punctuation", ThemeResourceKeyType.ForegroundColor);
        public static readonly ThemeResourceKey CommentForegroundColorKey = new(MefItemsCategory, "Comment", ThemeResourceKeyType.ForegroundColor);

        public static readonly ThemeResourceKey IdentifierBackgroundColorKey = new(MefItemsCategory, "Plain Text", ThemeResourceKeyType.BackgroundColor);
        public static readonly ThemeResourceKey ClassBackgroundColorKey = new(MefItemsCategory, "class name", ThemeResourceKeyType.BackgroundColor);
        public static readonly ThemeResourceKey EnumBackgroundColorKey = new(MefItemsCategory, "enum name", ThemeResourceKeyType.BackgroundColor);
        public static readonly ThemeResourceKey StructBackgroundColorKey = new(MefItemsCategory, "struct name", ThemeResourceKeyType.BackgroundColor);
        public static readonly ThemeResourceKey InterfaceBackgroundColorKey = new(MefItemsCategory, "interface name", ThemeResourceKeyType.BackgroundColor);
        public static readonly ThemeResourceKey KeywordBackgroundColorKey = new(MefItemsCategory, "Keyword", ThemeResourceKeyType.BackgroundColor);
        public static readonly ThemeResourceKey OperatorBackgroundColorKey = new(MefItemsCategory, "Operator", ThemeResourceKeyType.BackgroundColor);
        public static readonly ThemeResourceKey PunctuationBackgroundColorKey = new(MefItemsCategory, "punctuation", ThemeResourceKeyType.BackgroundColor);
        public static readonly ThemeResourceKey CommentBackgroundColorKey = new(MefItemsCategory, "Comment", ThemeResourceKeyType.BackgroundColor);

        public static readonly object TreeViewItemInactiveSelectedBackgroundKey = new ThemeResourceKey(TreeViewCategory, "SelectedItemInactive", ThemeResourceKeyType.BackgroundBrush);
    }
}
