using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using devsko.LayoutAnalyzer;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace LayoutAnalyzer
{
    public partial class MyToolWindowControl : UserControl
    {
        private IVsOutputWindowPane _outputWindowPane;
        private IVsFontAndColorStorage _fontAndColorStorage;

        public MyToolWindowControl((IVsOutputWindowPane, IVsFontAndColorStorage) tuple)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            (_outputWindowPane, _fontAndColorStorage) = tuple;

            InitializeComponent();

            ResetColors();

            // MOCK

            var tokenString = new TokenizedString(
                "devsko.LayoutAnalyzer.TestProject.TestClass",
                new[]
                {
                    new TokenSpan(Token.Identifier, 6),
                    new TokenSpan(Token.Operator, 1),
                    new TokenSpan(Token.Identifier, 14),
                    new TokenSpan(Token.Operator, 1),
                    new TokenSpan(Token.Identifier, 6),
                });

            typeBlock.Inlines.AddRange(ConvertTokenizedString(tokenString));
        }

        public void ResetColors()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Guid guid = DefGuidList.guidTextEditorFontCategory;
            _fontAndColorStorage.OpenCategory(ref guid, (uint)(__FCSTORAGEFLAGS.FCSF_READONLY | __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS));
            try
            {
                var info = new ColorableItemInfo[1];

                Resources[ForegroundColorKey] = GetForegroundColor("Plain Text");
                Resources[PunctuationColorKey] = GetForegroundColor("Punctuation");
                Resources[OperatorColorKey] = GetForegroundColor("Operator");

                Color GetForegroundColor(string name)
                {
                    _fontAndColorStorage.GetItem(name, info);
                    uint color = info[0].crForeground;
                    return Color.FromArgb(0xff, (byte)(color >> 0), (byte)(color >> 8), (byte)(color >> 16));
                }
            }
            finally
            {
                _fontAndColorStorage.CloseCategory();
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

        private IEnumerable<Run> ConvertTokenizedString(TokenizedString value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int index = 0;
            foreach (TokenSpan span in value.Tokens)
            {
                Run run = new(value.Value.Substring(index, span.Length));
                run.Foreground = (Brush)Resources[span.Token.ToString() + "Foreground"];
                yield return run;
                index += span.Length;
            }
        }
    }
}
