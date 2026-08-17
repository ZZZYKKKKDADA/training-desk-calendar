using TrainingDeskCalendar.App.Updates;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Updates;

public sealed class RepositoryMetadataTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo/")]
    public void Parse_AcceptsExactPublicGitHubRepository(string value)
    {
        RepositoryMetadata metadata = RepositoryMetadata.Parse(value);

        Assert.Equal("owner", metadata.Owner);
        Assert.Equal("repo", metadata.Repository);
        Assert.Equal("owner/repo", metadata.Slug);
        Assert.Equal(new Uri("https://github.com/owner/repo"), metadata.RepositoryUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://github.com/owner/repo")]
    [InlineData("https://example.com/owner/repo")]
    [InlineData("https://github.com/owner")]
    [InlineData("https://github.com/owner/repo/issues")]
    [InlineData("https://github.com/owner/repo?tab=readme")]
    [InlineData("https://user@github.com/owner/repo")]
    public void Parse_RejectsAnythingOutsideExactHttpsGitHubRepository(string value)
    {
        Assert.Throws<FormatException>(() => RepositoryMetadata.Parse(value));
    }

    [Fact]
    public void TryParse_ReturnsFalseWhenLocalBuildHasNoRepositoryMetadata()
    {
        Assert.False(RepositoryMetadata.TryParse(null, out RepositoryMetadata? metadata));
        Assert.Null(metadata);
    }
}
