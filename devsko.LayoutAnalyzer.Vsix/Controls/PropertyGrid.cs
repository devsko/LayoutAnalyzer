using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace devsko.LayoutAnalyzer
{
    public record Property(string Name, string BindingPath, IValueConverter Converter = null, string Format = null);

    public class StructLayoutAttributeConverter : IValueConverter
    {
        public static readonly StructLayoutAttributeConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Layout layout
                || layout.AttributeKind == (layout.IsValueType ? LayoutKind.Sequential : LayoutKind.Auto)
                    && layout.AttributeSize == 0
                    && layout.AttributePack == 8)
            {
                return string.Empty;
            }

            string result = $"LayoutKind.{layout.AttributeKind}";
            if (layout.AttributeSize != 0)
            {
                result += $", Size={layout.AttributeSize}";
            }
            if (layout.AttributePack != 8)
            {
                result += $", Pack={layout.AttributePack}";
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class PropertyGrid : Grid
    {
        public static Property[] DefaultProperties
            => new Property[]
            {
                new("Runtime", "Runtime"),
                new("Type", "Name.Value"),
                new("Assembly", "AssemblyName"),
                new("Path", "AssemblyPath"),
                new("StructLayout", "", Converter: StructLayoutAttributeConverter.Instance),
                new("Total size", "TotalSize", Format: "{0:N0} Bytes"),
                new("Total padding", "TotalPadding", Format: "{0:N0} Bytes"),
            };

        protected override void OnInitialized(EventArgs e)
        {
            ColumnDefinitions.Clear();
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            base.OnInitialized(e);
        }

        public Property[] Properties
        {
            get => (Property[])GetValue(PropertiesProperty);
            set => SetValue(PropertiesProperty, value);
        }

        public static readonly DependencyProperty PropertiesProperty =
            DependencyProperty.Register("Properties", typeof(Property[]), typeof(PropertyGrid), new PropertyMetadata(null, OnPropertiesChanged));

        private static void OnPropertiesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var @this = (PropertyGrid)sender;
            @this.RowDefinitions.Clear();

            var labelStyle = (Style)@this.FindResource("PropertyStyle");
            var valueStyle = (Style)@this.FindResource("PropertyValueStyle");

            int i = 0;
            foreach (Property property in (Property[])args.NewValue)
            {
                @this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new Label
                {
                    Style = labelStyle,
                    Content = property.Name,
                };

                var value = new Label
                {
                    Style = valueStyle,
                    Content = new TextBlock(),
                };
                var binding = new Binding
                {
                    Path = new PropertyPath(property.BindingPath),
                    StringFormat = property.Format,
                    Converter = property.Converter,
                };
                ((FrameworkElement)value.Content).SetBinding(TextBlock.TextProperty, binding);

                Grid.SetRow(label, i);
                Grid.SetRow(value, i);

                @this.Children.Add(label);
                @this.Children.Add(value);

                i++;
            }
        }
    }
}
