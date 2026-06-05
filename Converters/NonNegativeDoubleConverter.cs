using System;
using System.Globalization;
using System.Windows.Data;

namespace NMEAReceiver.Converters;

[ValueConversion(typeof(double), typeof(double))]
public sealed class NonNegativeDoubleConverter : IValueConverter
{
    public static readonly NonNegativeDoubleConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d && d < 0 ? 0.0 : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value;
}
