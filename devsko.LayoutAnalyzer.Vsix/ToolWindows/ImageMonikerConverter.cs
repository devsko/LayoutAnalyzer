﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

namespace devsko.LayoutAnalyzer
{
    public class ImageMonikerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Token token)
            {
                return default(ImageMoniker);
            }

            return token switch
            {
                Token.Class => KnownMonikers.ClassPublic,
                Token.Struct => KnownMonikers.StructurePublic,
                Token.Enum => KnownMonikers.EnumerationPublic,
                Token.Interface => KnownMonikers.InterfacePublic,
                Token.Delegate => KnownMonikers.DelegatePublic,
                _ => KnownMonikers.Blank,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
