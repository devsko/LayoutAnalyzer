using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using devsko.LayoutAnalyzer;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace LayoutAnalyzer
{
    public partial class LayoutControl : UserControl
    {
        public LayoutControl(LayoutAnalyzerPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            InitializeComponent();

            package.TextManagerEventSink.ColorsChanged += (sender, args) => ResetColors(package);
            ResetColors(package);

            // MOCK

            var tokenString = new TokenizedString(
                "devsko.LayoutAnalyzer.TestProject.TestClass",
                new[]
                {
                    new TokenSpan(Token.Identifier, 6),
                    new TokenSpan(Token.Operator, 1),
                    new TokenSpan(Token.Identifier, 14),
                    new TokenSpan(Token.Operator, 1),
                    new TokenSpan(Token.Identifier, 11),
                    new TokenSpan(Token.Operator, 1),
                    new TokenSpan(Token.Class, 9),
                });

            typeBlock.Inlines.AddRange(ConvertTokenizedString(tokenString));

            IEnumerable<Run> ConvertTokenizedString(TokenizedString value)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                int index = 0;
                foreach (TokenSpan span in value.Tokens)
                {
                    Run run = new(value.Value.Substring(index, span.Length));
                    run.Foreground = (Brush)Resources[span.Token.ToString() + "Foreground"];
                    run.Background = (Brush)Resources[span.Token.ToString() + "Background"];
                    yield return run;
                    index += span.Length;
                }
            }
        }

        private void ResetColors(LayoutAnalyzerPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Guid guid = DefGuidList.guidTextEditorFontCategory;
            package.FontAndColorStorage.OpenCategory(ref guid, (uint)(__FCSTORAGEFLAGS.FCSF_READONLY | __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS));
            try
            {
                var info = new ColorableItemInfo[1];

                SetResources(IdentifierForegroundColorKey, IdentifierBackgroundColorKey);
                SetResources(ClassForegroundColorKey, ClassBackgroundColorKey);
                SetResources(StructForegroundColorKey, StructBackgroundColorKey);
                SetResources(EnumForegroundColorKey, EnumBackgroundColorKey);
                SetResources(InterfaceForegroundColorKey, InterfaceBackgroundColorKey);
                SetResources(KeywordForegroundColorKey, KeywordBackgroundColorKey);
                SetResources(OperatorForegroundColorKey, OperatorBackgroundColorKey);
                SetResources(PunctuationForegroundColorKey, PunctuationBackgroundColorKey);

                void SetResources(ThemeResourceKey foreground, ThemeResourceKey background)
                    => (Resources[foreground], Resources[background]) = GetColor(foreground.Name);

                (Color, Color) GetColor(string name)
                {
                    package.FontAndColorStorage.GetItem(name, info);
                    uint foreground = info[0].crForeground;
                    uint background = info[0].crBackground;
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

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    ThreadHelper.ThrowIfNotOnUIThread();

        //    _outputWindowPane.Clear();
        //    _outputWindowPane.Activate();

        //    var current = VsColors.GetCurrentThemedColorValues();

        //    foreach (var group in current.GroupBy(kvp => kvp.Key.Category))
        //    {
        //        _outputWindowPane.OutputString(group.Key.ToString() + Environment.NewLine);
        //        foreach (var kvp in group)
        //        {
        //            var resourceKey = new ThemeResourceKey(group.Key, kvp.Key.Name, kvp.Key.KeyType);
        //            var value = FindResource(resourceKey);
        //            _outputWindowPane.OutputString($"      {kvp.Key.Name} {kvp.Key.KeyType}: {value}{Environment.NewLine}");
        //        }
        //    }
        //}
    }
}
