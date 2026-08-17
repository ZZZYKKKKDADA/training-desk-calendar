using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TrainingDeskCalendar.App.Domain;

namespace TrainingDeskCalendar.App.Calendar;

internal static class TaskColorPalette
{
    public static string GetHex(TaskColorId color) => color switch
    {
        TaskColorId.Teal => "#BFE3DA",
        TaskColorId.Blue => "#C7D8F2",
        TaskColorId.Orange => "#F4D1A6",
        TaskColorId.Red => "#F1C2C2",
        TaskColorId.Purple => "#D9C7E8",
        TaskColorId.Gray => "#D5DADF",
        _ => throw new ArgumentOutOfRangeException(nameof(color))
    };
}

public sealed class TaskColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TaskColorId color)
        {
            return Binding.DoNothing;
        }

        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(TaskColorPalette.GetHex(color))!);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class TaskColorSelectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TaskColorId color &&
        int.TryParse(parameter?.ToString(), out int selected) &&
        (int)color == selected;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
