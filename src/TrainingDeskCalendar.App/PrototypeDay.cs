using System.Windows.Media;

namespace TrainingDeskCalendar.App;

public sealed record PrototypeDay(
    string Weekday,
    int DayNumber,
    string Plan,
    System.Windows.Media.Brush Background,
    bool IsToday);
