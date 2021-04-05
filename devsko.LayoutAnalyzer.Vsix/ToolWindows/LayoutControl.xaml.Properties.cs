#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualStudio.Shell;

namespace devsko.LayoutAnalyzer
{
    partial class LayoutControl
    {
        private class StructLayoutAttributeConverter : IValueConverter
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

        private record Property(string Name, string BindingPath, IValueConverter? Converter = null, string? Format = null);

        private void CreatePropertyGrid()
        {
            var properties = new Property[]
            {
                new("Runtime", "Runtime"),
                new("Type", "Name.Value"),
                new("Assembly", "AssemblyName"),
                new("Path", "AssemblyPath"),
                new("StructLayout", "", Converter: StructLayoutAttributeConverter.Instance),
                new("Total size", "TotalSize", Format: "{0:N0} Bytes"),
                new("Total padding", "TotalPadding", Format: "{0:N0} Bytes"),
            };

            var labelStyle = (Style)Resources["PropertyStyle"];
            var valueStyle = (Style)Resources["PropertyValueStyle"];

            int i = 0;
            foreach (Property property in properties)
            {
                propertyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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

                propertyGrid.Children.Add(label);
                propertyGrid.Children.Add(value);

                i++;
            }
        }
    }
}
