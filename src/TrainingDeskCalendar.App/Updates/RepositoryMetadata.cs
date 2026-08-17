namespace TrainingDeskCalendar.App.Updates;

internal sealed record RepositoryMetadata(string Owner, string Repository)
{
    public string Slug => $"{Owner}/{Repository}";
    public Uri RepositoryUri => new($"https://github.com/{Slug}");

    public static RepositoryMetadata Parse(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new FormatException("Repository URL must be an HTTPS github.com repository.");
        }

        string[] segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 ||
            !IsCanonicalSegment(segments[0]) ||
            !IsCanonicalSegment(segments[1]))
        {
            throw new FormatException("Repository URL must contain exactly an owner and repository.");
        }

        return new RepositoryMetadata(segments[0], segments[1]);
    }

    public static bool TryParse(string? value, out RepositoryMetadata? metadata)
    {
        try
        {
            metadata = value is null ? null : Parse(value);
            return metadata is not null;
        }
        catch (FormatException)
        {
            metadata = null;
            return false;
        }
    }

    private static bool IsCanonicalSegment(string value) =>
        value.Length > 0 && value.All(character =>
            character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or
                '-' or '_' or '.');
}
