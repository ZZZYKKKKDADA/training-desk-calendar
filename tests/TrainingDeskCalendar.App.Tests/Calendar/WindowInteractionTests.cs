using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Windowing;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Calendar;
using System.Windows;
using System.Xml.Linq;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Calendar;

public sealed class WindowInteractionTests
{
    [Fact]
    public void MainWindow_CompletedBinding_IsExplicitlyOneWay()
    {
        string solutionRoot = FindSolutionRoot();
        XDocument document = XDocument.Load(Path.Combine(
            solutionRoot,
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XElement completedCheckBox = Assert.Single(
            document.Descendants(presentation + "CheckBox"),
            element => (string?)element.Attribute("ToolTip") == "切换完成状态");

        Assert.Equal(
            "{Binding IsCompleted, Mode=OneWay}",
            (string?)completedCheckBox.Attribute("IsChecked"));
    }

    [Fact]
    public void Application_UsesExplicitShutdownMode()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "App.xaml"));

        Assert.Equal(
            "OnExplicitShutdown",
            (string?)document.Root?.Attribute("ShutdownMode"));
    }

    [Fact]
    public void SettingsWindow_ReadOnlyPropertiesUseOneWayBindings()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "Settings",
            "SettingsWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XElement versionText = Assert.Single(
            document.Descendants(presentation + "TextBlock"),
            element => ((string?)element.Attribute("Text"))?.Contains("VersionText") == true);
        XElement repositoryRun = Assert.Single(
            document.Descendants(presentation + "Run"),
            element => ((string?)element.Attribute("Text"))?.Contains("RepositoryText") == true);
        XElement repositoryLink = Assert.Single(
            document.Descendants(presentation + "Hyperlink"));

        Assert.Equal("{Binding VersionText, Mode=OneWay}", (string?)versionText.Attribute("Text"));
        Assert.Equal("{Binding RepositoryText, Mode=OneWay}", (string?)repositoryRun.Attribute("Text"));
        Assert.Equal("{Binding CanOpenRepository, Mode=OneWay}", (string?)repositoryLink.Attribute("IsEnabled"));
    }

    [Fact]
    public void WindowClosePolicy_HidesUntilExplicitExitIsRequested()
    {
        var policy = new WindowClosePolicy();

        Assert.True(policy.ShouldHide);
        policy.RequestExit();
        Assert.False(policy.ShouldHide);
    }

    [Theory]
    [InlineData(1, "#BFE3DA")]
    [InlineData(2, "#C7D8F2")]
    [InlineData(3, "#F4D1A6")]
    [InlineData(4, "#F1C2C2")]
    [InlineData(5, "#D9C7E8")]
    [InlineData(6, "#D5DADF")]
    public void TaskColorPalette_MapsTheSixApprovedCardFills(
        int colorId,
        string expectedHex)
    {
        Assert.Equal(expectedHex, TaskColorPalette.GetHex((TaskColorId)colorId));
    }

    [Fact]
    public void MainWindow_ProvidesHeaderControlsAndSixColorRadioButtons()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.Contains(
            document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("ToolTip") == "锁定或解锁组件");
        Assert.Contains(
            document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("ToolTip") == "最小化");
        Assert.Equal(
            6,
            document.Descendants(presentation + "RadioButton")
                .Count(element => element.Attribute("Tag") is not null));
        Assert.DoesNotContain(
            document.Descendants(presentation + "RadioButton"),
            element => element.Attribute("GroupName") is not null);

        XElement card = Assert.Single(
            document.Descendants(presentation + "Border"),
            element => element.Attribute("MouseLeftButtonUp") is not null);
        Assert.Contains("SelectedColor", (string?)card.Attribute("Background"));
    }

    [Fact]
    public void ApplicationExit_DoesNotSynchronouslyBlockOnAsyncDisposal()
    {
        string source = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "App.xaml.cs"));

        Assert.DoesNotContain("GetAwaiter().GetResult()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LockedState_DisablesMovementAndResizeTogether()
    {
        var state = new WindowInteractionState();

        Assert.True(state.CanMove);
        Assert.True(state.CanResize);
        state.SetLocked(true);

        Assert.True(state.IsLocked);
        Assert.False(state.CanMove);
        Assert.False(state.CanResize);
        state.SetLocked(false);
        Assert.True(state.CanMove);
        Assert.True(state.CanResize);
    }

    [Fact]
    public void WindowDragService_TracksPointerDeltaUntilDragEnds()
    {
        var service = new WindowDragService();
        service.Begin(new Point(400, 220), new Point(100, 80));

        Assert.True(service.TryGetPosition(new Point(650, 440), out Point position));
        Assert.Equal(new Point(350, 300), position);

        service.End();
        Assert.False(service.TryGetPosition(new Point(700, 500), out _));
    }

    [Fact]
    public void MainWindow_HeaderUsesManualDragHandlers()
    {
        string solutionRoot = FindSolutionRoot();
        string xamlPath = Path.Combine(
            solutionRoot,
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml");
        XDocument document = XDocument.Load(xamlPath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XElement header = Assert.Single(
            document.Descendants(presentation + "DockPanel"),
            element => (string?)element.Attribute("MouseLeftButtonDown") ==
                "OnHeaderMouseLeftButtonDown");

        Assert.Equal("OnHeaderMouseMove", (string?)header.Attribute("MouseMove"));
        Assert.Equal("OnHeaderMouseLeftButtonUp", (string?)header.Attribute("MouseLeftButtonUp"));
        Assert.Equal("OnHeaderLostMouseCapture", (string?)header.Attribute("LostMouseCapture"));

        string codeBehind = File.ReadAllText(Path.ChangeExtension(xamlPath, ".xaml.cs"));
        Assert.DoesNotContain("DragMove();", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_DoesNotPeriodicallyReattachToDesktop()
    {
        string source = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml.cs"));

        Assert.DoesNotContain("desktopWatchdog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnDesktopWatchdogTick", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_SavesEditingCardWhenClickingOutsideIt()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml"));

        Assert.Equal(
            "OnWindowPreviewMouseLeftButtonDown",
            (string?)document.Root?.Attribute("PreviewMouseLeftButtonDown"));

        string source = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml.cs"));
        Assert.Contains("SaveEditAsync(editingCard)", source, StringComparison.Ordinal);
        Assert.Contains("IsWithinCard", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_DoesNotRenderDesktopFallbackStatus()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.DoesNotContain(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute(xaml + "Name") == "DesktopStatusText");

        string source = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml.cs"));

        Assert.DoesNotContain("桌面层：", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DesktopStatusText", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ProvidesMinimizeAndCloseButtons()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.Contains(
            document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("ToolTip") == "最小化");
        Assert.Contains(
            document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("ToolTip") == "关闭程序");

        string source = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml.cs"));
        Assert.Contains("WindowState = WindowState.Minimized", source, StringComparison.Ordinal);
        Assert.Contains("RequestExitAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TrayShow_RestoresAMinimizedWindow()
    {
        string source = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "App.xaml.cs"));

        Assert.Contains("window.WindowState = WindowState.Normal", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_DatesUseArrowsWithoutTodayButtonAndDarkRangeText()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.DoesNotContain(
            document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Content") == "今天");

        XElement rangeLabel = Assert.Single(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding RangeLabel}");
        Assert.Equal("#20262B", (string?)rangeLabel.Attribute("Foreground"));
    }

    [Fact]
    public void MainWindow_IsExplicitlyNonTopmost()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml"));

        Assert.Equal("False", (string?)document.Root?.Attribute("Topmost"));

        string source = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "Desktop",
            "Win32DesktopWindowApi.cs"));
        Assert.Contains("HwndNoTopmost", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "#F7F8FA", "#20262B")]
    [InlineData(1, "#20262B", "#F7F8FA")]
    public void AppearancePalette_UsesContrastingSurfaceAndText(
        int themeId,
        string surface,
        string foreground)
    {
        AppTheme theme = (AppTheme)themeId;
        AppearancePalette palette = AppearancePalette.Create(theme, 0.75);

        Assert.Equal(surface, palette.SurfaceHex);
        Assert.Equal(foreground, palette.ForegroundHex);
        Assert.Equal(0.75, palette.Opacity);
        Assert.NotEqual(palette.SurfaceHex, palette.ForegroundHex);
    }

    [Theory]
    [InlineData(0.39)]
    [InlineData(1.01)]
    public void AppearancePalette_RejectsOpacityOutsideApprovedRange(double opacity)
    {
        Assert.Throws<InvalidDataException>(() => AppearancePalette.Create(AppTheme.Light, opacity));
    }

    [Fact]
    public void WindowStateService_RoundTripsPlacementAndMonitor()
    {
        AppSettings settings = AppSettings.Defaults with
        {
            WindowX = 30,
            WindowY = 40,
            WindowWidth = 960,
            WindowHeight = 420,
            MonitorId = "monitor-1"
        };
        var service = new WindowStateService();

        WindowPlacement placement = service.ToPlacement(settings);
        AppSettings updated = service.WithPlacement(settings, placement with
        {
            X = 80,
            Y = 90,
            Width = 1000,
            Height = 440,
            MonitorId = "monitor-2"
        });

        Assert.Equal("monitor-2", updated.MonitorId);
        Assert.Equal(80, updated.WindowX);
        Assert.Equal(440, updated.WindowHeight);
        Assert.Equal(1, updated.Version);
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "TrainingDeskCalendar.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException(
            "Unable to locate TrainingDeskCalendar.sln from the test output directory.");
    }
}
