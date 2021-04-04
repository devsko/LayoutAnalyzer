#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
        private record Property(string Name, string BindingPath, string? Format = null);

        private void CreatePropertyGrid()
        {
            var properties = new Property[]
            {
                new("Runtime", "Runtime"),
                new("Type", "Name.Value"),
                new("Assembly", "AssemblyName"),
                new("Path", "AssemblyPath"),
                new("Total size", "TotalSize", "{0:N0} Bytes"),
                new("Total padding", "TotalPadding", "{0:N0} Bytes"),

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
