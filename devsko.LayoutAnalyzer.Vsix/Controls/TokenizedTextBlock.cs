using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace devsko.LayoutAnalyzer
{
    public class TokenizedTextBlock : TextBlock
    {
        public TokenizedString String
        {
            get => (TokenizedString)GetValue(StringProperty);
            set => SetValue(StringProperty, value);
        }

        public static readonly DependencyProperty StringProperty =
            DependencyProperty.Register("String", typeof(TokenizedString), typeof(TokenizedTextBlock), new PropertyMetadata(default(TokenizedString), StringChanged));

        private static void StringChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var @this = (TokenizedTextBlock)sender;
            @this.Inlines.Clear();
            var value = (TokenizedString)args.NewValue;
            if (value.Value is not null && value.Tokens is not null)
            {
                int index = 0;
                foreach (TokenSpan span in value.Tokens)
                {
                    Run run = new(value.Value.Substring(index, span.Length));
                    string tokenString = span.Token.ToString();
                    run.Foreground = (Brush)@this.FindResource(tokenString + "Foreground");
                    run.Background = (Brush)@this.FindResource(tokenString + "Background");
                    @this.Inlines.Add(run);
                    index += span.Length;
                }
            }
        }
    }
}
