using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace devsko.LayoutAnalyzer
{
    public class LayoutTreeView : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride()
            => new LayoutTreeViewItem();
    }

    public class LayoutTreeViewItem : TreeViewItem
    {
        public class MinWidthConverter : IValueConverter
        {
            public static readonly MinWidthConverter Instance = new();

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var level = (int)value;
                return
                    level == 0 ? 0 :
                    level == 1 ? 19 :
                    87;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotImplementedException();
        }

        public int Level
        {
            get { return (int)GetValue(LevelProperty); }
            set { SetValue(LevelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Level.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register("Level", typeof(int), typeof(LayoutTreeViewItem), new PropertyMetadata(0, OnLevelChanged));

        private static void OnLevelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
            => ((LayoutTreeViewItem)sender).IsTopLevel = (int)args.NewValue == 0;

        public bool IsTopLevel
        {
            get => (bool)GetValue(IsTopLevelProperty);
            set => SetValue(IsTopLevelProperty, value);
        }

        public static readonly DependencyProperty IsTopLevelProperty =
            DependencyProperty.Register("IsTopLevel", typeof(bool), typeof(LayoutTreeViewItem), new PropertyMetadata(false));

        public LayoutTreeViewItem()
        {
            IsTopLevel = true;
        }

        public LayoutTreeViewItem(int level)
        {
            Level = level;
        }

        protected override DependencyObject GetContainerForItemOverride()
            => new LayoutTreeViewItem(Level + 1);
    }
}
