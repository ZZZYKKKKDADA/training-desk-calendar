using TrainingDeskCalendar.App.Updates;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Updates;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.2.3")]
    [InlineData("1.2.3")]
    public void Parse_AcceptsStableThreePartVersions(string value)
    {
        ReleaseVersion version = ReleaseVersion.Parse(value);

        Assert.Equal(1, version.Major);
        Assert.Equal(2, version.Minor);
        Assert.Equal(3, version.Patch);
        Assert.Equal("1.2.3", version.ToString());
    }

    [Fact]
    public void CompareTo_OrdersMajorMinorAndPatch()
    {
        var current = new ReleaseVersion(1, 2, 3);

        Assert.True(new ReleaseVersion(2, 0, 0) > current);
        Assert.True(new ReleaseVersion(1, 3, 0) > current);
        Assert.True(new ReleaseVersion(1, 2, 4) > current);
        Assert.Equal(current, ReleaseVersion.Parse("v1.2.3"));
        Assert.True(new ReleaseVersion(1, 2, 2) < current);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("v1.2.3-beta")]
    [InlineData("1.2.3+build")]
    [InlineData("-1.2.3")]
    [InlineData("v01.2.3")]
    public void Parse_RejectsNonReleaseVersions(string value)
    {
        Assert.Throws<FormatException>(() => ReleaseVersion.Parse(value));
    }
}
