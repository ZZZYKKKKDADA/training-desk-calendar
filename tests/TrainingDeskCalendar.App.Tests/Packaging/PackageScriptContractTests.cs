using Xunit;

namespace TrainingDeskCalendar.App.Tests.Packaging;

public sealed class PackageScriptContractTests
{
    [Fact]
    public void Script_PublishesSelfContainedDirectoryAndEnforcesInstalledSize()
    {
        string script = ReadScript();

        Assert.Contains("eng\\Versions.props", script, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("-p:SatelliteResourceLanguages=zh-Hans", script, StringComparison.Ordinal);
        Assert.Contains("-p:PublishSingleFile=true", script, StringComparison.Ordinal);
        Assert.Contains("-p:IncludeNativeLibrariesForSelfExtract=true", script, StringComparison.Ordinal);
        Assert.Contains("-p:EnableCompressionInSingleFile=false", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item -LiteralPath $windowsSdkProjection", script, StringComparison.Ordinal);
        Assert.Contains("150MB", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-RestMethod", script, StringComparison.Ordinal);
        Assert.DoesNotContain("windowsdesktop-runtime", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_PublishesApplicationDirectlyIntoFixedPayload()
    {
        string script = ReadScript();

        Assert.Contains("TrainingDeskCalendar.App.csproj", script, StringComparison.Ordinal);
        Assert.Contains("artifacts\\windows-x64\\payload", script, StringComparison.Ordinal);
        Assert.Contains("TrainingDeskCalendar.App.exe", script, StringComparison.Ordinal);
        Assert.Contains("$publishedFiles.Count -ne 1", script, StringComparison.Ordinal);
        Assert.Contains("package-manifest.json", script, StringComparison.Ordinal);
        Assert.Contains("self-contained-single-file-uncompressed", script, StringComparison.Ordinal);
        Assert.Contains("while ($payloadBytes -ne $manifest.payloadBytes)", script, StringComparison.Ordinal);
        Assert.Contains("$manifestWriteAttempts -gt 5", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Join-Path $payloadPath 'runtime'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Application_TargetsWpfWithoutVersionedWindowsSdkProjection()
    {
        string project = ReadRepositoryFile(
            "src",
            "TrainingDeskCalendar.App",
            "TrainingDeskCalendar.App.csproj");

        Assert.Contains("<TargetFramework>net10.0-windows</TargetFramework>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<SupportedOSPlatformVersion>", project, StringComparison.Ordinal);
    }

    private static string ReadScript()
    {
        return ReadRepositoryFile("scripts", "package-windows.ps1");
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null &&
               !File.Exists(Path.Combine(directory, "TrainingDeskCalendar.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(new[] { directory }.Concat(pathParts).ToArray()));
    }
}
