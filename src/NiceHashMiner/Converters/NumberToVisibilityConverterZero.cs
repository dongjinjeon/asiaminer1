﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace NiceHashMiner.Converters
{
    class NumberToVisibilityConverterZero : IValueConverter
    {
        /// <summary>
        /// Convert an object to visibility, with the basis that null is Visbility.Collapsed.
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <param name="parameter">String "1" to invert the answer</param>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
            {
                return i == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            // Explicit XAML parameters will come in as strings, here we say if it is a number > 0 
            // then we invert (show the opposite visibility)
            var invert = parameter is string s && int.TryParse(s, out var inv) && inv > 0;

            // XOR will use the opposite answer if invert is true
            var ret = value == null ^ invert ? Visibility.Collapsed : Visibility.Visible;
            return ret;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
