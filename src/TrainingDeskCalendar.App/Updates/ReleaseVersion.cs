namespace TrainingDeskCalendar.App.Updates;

internal readonly record struct ReleaseVersion : IComparable<ReleaseVersion>
{
    public ReleaseVersion(int major, int minor, int patch)
    {
        if (major < 0 || minor < 0 || patch < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major));
        }

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public static ReleaseVersion Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new FormatException("Release version is empty.");
        }

        ReadOnlySpan<char> version = value.AsSpan();
        if (version[0] == 'v') version = version[1..];
        Span<Range> ranges = stackalloc Range[4];
        int count = version.Split(ranges, '.', StringSplitOptions.None);
        if (count != 3)
        {
            throw new FormatException("Release version must contain exactly three numeric parts.");
        }

        return new ReleaseVersion(
            ParsePart(version[ranges[0]]),
            ParsePart(version[ranges[1]]),
            ParsePart(version[ranges[2]]));
    }

    public int CompareTo(ReleaseVersion other)
    {
        int major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        int minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool operator <(ReleaseVersion left, ReleaseVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator >(ReleaseVersion left, ReleaseVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) =>
        left.CompareTo(right) >= 0;

    private static int ParsePart(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || (value.Length > 1 && value[0] == '0'))
        {
            throw new FormatException("Release version parts must use canonical decimal notation.");
        }

        foreach (char character in value)
        {
            if (character is < '0' or > '9')
            {
                throw new FormatException("Release version contains a non-numeric part.");
            }
        }

        if (!int.TryParse(value, out int result))
        {
            throw new FormatException("Release version part is outside the supported range.");
        }

        return result;
    }
}
