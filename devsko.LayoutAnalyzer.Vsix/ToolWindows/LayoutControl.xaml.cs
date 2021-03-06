using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace devsko.LayoutAnalyzer
{
    public partial class LayoutControl : UserControl
    {
        public LayoutControl(LayoutAnalyzerPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            InitializeComponent();

            package.TextManagerEventSink.ColorsChanged += () => ResetColors(package);
            ResetColors(package);


            DataContextChanged += (sender, args) =>
            {
                if (args.NewValue is not Layout layout)
                {
                    tree.ItemsSource = null;
                    return;
                }

                tree.ItemContainerGenerator.StatusChanged += OnGeneratorStatusChanged;
                tree.ItemsSource = new[] { layout };

                static void OnGeneratorStatusChanged(object sender, EventArgs args)
                {
                    var generator = (ItemContainerGenerator)sender;
                    if (generator.Status >= GeneratorStatus.ContainersGenerated)
                    {
                        var item = (TreeViewItem)generator.ContainerFromIndex(0);
                        if (item is not null)
                        {
                            item.IsExpanded = true;
                        }
                        generator.StatusChanged -= OnGeneratorStatusChanged;
                    }
                }
            };
        }

        private void ResetColors(LayoutAnalyzerPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Guid guid = DefGuidList.guidTextEditorFontCategory;
            package.FontAndColorStorage.OpenCategory(ref guid, (uint)(__FCSTORAGEFLAGS.FCSF_READONLY | __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS));
            try
            {
                var fontInfo = new FontInfo[1];

                package.FontAndColorStorage.GetFont(null, fontInfo);

                Resources["CodeFontFamily"] = new FontFamily(fontInfo[0].bstrFaceName);

                var colorInfo = new ColorableItemInfo[1];

                SetResources(IdentifierForegroundColorKey, IdentifierBackgroundColorKey);
                SetResources(ClassForegroundColorKey, ClassBackgroundColorKey);
                SetResources(StructForegroundColorKey, StructBackgroundColorKey);
                SetResources(DelegateForegroundColorKey, DelegateBackgroundColorKey);
                SetResources(EnumForegroundColorKey, EnumBackgroundColorKey);
                SetResources(InterfaceForegroundColorKey, InterfaceBackgroundColorKey);
                SetResources(KeywordForegroundColorKey, KeywordBackgroundColorKey);
                SetResources(OperatorForegroundColorKey, OperatorBackgroundColorKey);
                SetResources(PunctuationForegroundColorKey, PunctuationBackgroundColorKey);
                SetResources(CommentForegroundColorKey, CommentBackgroundColorKey);

                void SetResources(ThemeResourceKey foreground, ThemeResourceKey background)
                    => (Resources[foreground], Resources[background]) = GetColor(foreground.Name);

                (Color, Color) GetColor(string name)
                {
                    package.FontAndColorStorage.GetItem(name, colorInfo);
                    uint foreground = colorInfo[0].crForeground;
                    uint background = colorInfo[0].crBackground;
                    return (
                        Color.FromArgb(0xff, (byte)(foreground >> 0), (byte)(foreground >> 8), (byte)(foreground >> 16)),
                        Color.FromArgb(0xff, (byte)(background >> 0), (byte)(background >> 8), (byte)(background >> 16)));
                }
            }
            finally
            {
                package.FontAndColorStorage.CloseCategory();
            }
        }
    }
}
