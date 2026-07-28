using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WrightLogs.Models;

namespace WrightLogs.Converters;

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public static readonly LogLevelToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        LogLevel.Warning => Brushes.DarkOrange,
        LogLevel.Error => Brushes.Red,
        LogLevel.Fatal => Brushes.DarkRed,
        LogLevel.Debug or LogLevel.Verbose => Brushes.Gray,
        _ => AvaloniaProperty.UnsetValue,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
