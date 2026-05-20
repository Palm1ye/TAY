using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace TAY.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool flag = value is bool b && b;
            if (Invert) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility v)
            {
                bool flag = v == Visibility.Visible;
                return Invert ? !flag : flag;
            }

            return false;
        }
    }
}
