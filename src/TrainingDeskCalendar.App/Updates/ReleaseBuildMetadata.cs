using System.Reflection;

namespace TrainingDeskCalendar.App.Updates;

internal sealed record ReleaseBuildMetadata(
    ReleaseVersion Version,
    RepositoryMetadata? Repository)
{
    public static ReleaseBuildMetadata FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        string? repositoryUrl = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .LastOrDefault(attribute => attribute.Key == "RepositoryUrl")?
            .Value;
        return FromValues(assembly.GetName().Version, repositoryUrl);
    }

    public static ReleaseBuildMetadata FromValues(
        Version? assemblyVersion,
        string? repositoryUrl)
    {
        if (assemblyVersion is null || assemblyVersion.Build < 0)
        {
            throw new FormatException("Assembly version must contain at least three parts.");
        }

        var version = new ReleaseVersion(
            assemblyVersion.Major,
            assemblyVersion.Minor,
            assemblyVersion.Build);
        RepositoryMetadata? repository = repositoryUrl is null
            ? null
            : RepositoryMetadata.Parse(repositoryUrl);
        return new ReleaseBuildMetadata(version, repository);
    }
}
