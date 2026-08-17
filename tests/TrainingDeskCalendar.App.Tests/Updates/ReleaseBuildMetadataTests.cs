using System.Reflection;
using System.Reflection.Emit;
using TrainingDeskCalendar.App.Updates;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Updates;

public sealed class ReleaseBuildMetadataTests
{
    [Fact]
    public void FromValues_UsesThreePartAssemblyVersionAndOptionalRepository()
    {
        ReleaseBuildMetadata metadata = ReleaseBuildMetadata.FromValues(
            new Version(1, 2, 3, 4),
            "https://github.com/owner/repo");

        Assert.Equal(new ReleaseVersion(1, 2, 3), metadata.Version);
        Assert.Equal("owner/repo", metadata.Repository?.Slug);
    }

    [Fact]
    public void FromValues_AllowsLocalBuildWithoutRepository()
    {
        ReleaseBuildMetadata metadata = ReleaseBuildMetadata.FromValues(
            new Version(0, 1, 0, 0),
            repositoryUrl: null);

        Assert.Equal(new ReleaseVersion(0, 1, 0), metadata.Version);
        Assert.Null(metadata.Repository);
    }

    [Fact]
    public void FromAssembly_ReadsRepositoryUrlAssemblyMetadata()
    {
        var name = new AssemblyName("ReleaseBuildMetadataFixture")
        {
            Version = new Version(2, 3, 4, 0)
        };
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            name,
            AssemblyBuilderAccess.Run);
        ConstructorInfo constructor = typeof(AssemblyMetadataAttribute).GetConstructor(
            [typeof(string), typeof(string)])!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            constructor,
            ["RepositoryUrl", "https://github.com/example/calendar"]));

        ReleaseBuildMetadata metadata = ReleaseBuildMetadata.FromAssembly(assembly);

        Assert.Equal(new ReleaseVersion(2, 3, 4), metadata.Version);
        Assert.Equal("example/calendar", metadata.Repository?.Slug);
    }

    [Fact]
    public void ApplicationProject_EmitsInjectedRepositoryUrlAsAssemblyMetadata()
    {
        string project = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "TrainingDeskCalendar.App.csproj"));

        Assert.Contains("AssemblyMetadataAttribute", project, StringComparison.Ordinal);
        Assert.Contains("$(RepositoryUrl)", project, StringComparison.Ordinal);
        Assert.Contains("Condition=\"'$(RepositoryUrl)' != ''\"", project, StringComparison.Ordinal);
        Assert.DoesNotContain("https://github.com/", project, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindSolutionRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null &&
               !File.Exists(Path.Combine(directory, "TrainingDeskCalendar.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new DirectoryNotFoundException();
    }
}
