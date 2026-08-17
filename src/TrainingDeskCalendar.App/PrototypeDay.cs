using System.Windows.Media;

namespace TrainingDeskCalendar.App;

public sealed record PrototypeDay(
    string Weekday,
    int DayNumber,
    string Plan,
    Brush Background,
    bool IsToday);
