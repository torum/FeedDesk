using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace FeedDesk.Helpers;

public partial class BooleanToVisibilityCollapsedConverter : IValueConverter
{
    public Visibility TrueValue { get; set; }
    public Visibility FalseValue { get; set; }

    public BooleanToVisibilityCollapsedConverter()
    {
        // set defaults
        TrueValue = Visibility.Visible;
        FalseValue = Visibility.Collapsed;
    }

    public object? Convert(object value, Type targetType,
        object parameter, string language)
    {
        if (value is not bool)
            return null;
        return (bool)value ? TrueValue : FalseValue;
    }

    public object? ConvertBack(object value, Type targetType,
        object parameter, string language)
    {
        if (Equals(value, TrueValue))
            return true;
        if (Equals(value, FalseValue))
            return false;
        return null;
    }
}
