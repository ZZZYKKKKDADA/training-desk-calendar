# Training Desk Calendar Windows Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a minimal WPF prototype that proves the Windows desktop-host behavior and measures the approved startup, CPU, memory, and publish-size targets before production feature work begins.

**Architecture:** A single WPF executable owns a transparent two-week sample window. Desktop attachment is isolated behind an API interface and a small state service so failure and fallback behavior can be unit tested without invoking Win32. A PowerShell harness publishes both framework-dependent and self-contained variants, launches the prototype repeatedly, and writes measured results for the packaging decision.

**Tech Stack:** C# 14, WPF, .NET 10, xUnit, Win32 P/Invoke, PowerShell 7, Git.

---

## Scope Boundary

This plan intentionally does not implement editable plans, SQLite, tray menus, startup registration, backups, updates, installers, or GitHub publishing. Those belong to later phases after this prototype passes.

The prototype is complete only when it demonstrates all of the following on the required Windows test matrix:

- The window appears on the desktop layer, has no taskbar button, is not always-on-top, and is covered by ordinary application windows.
- The process survives Windows Explorer restart and reconnects to the desktop host.
- A failed desktop attachment produces a visible non-topmost fallback window instead of losing the application.
- Window placement remains visible when a monitor disappears and uses device-independent coordinates.
- Cold startup, idle CPU, idle memory, and publish sizes are measured by a repeatable script.
- A written gate decision records whether the project can continue with WPF and which .NET 10 packaging mode will be used.

## Planned File Structure

```text
TrainingDeskCalendar.sln
global.json
Directory.Build.props
src/
  TrainingDeskCalendar.App/
    TrainingDeskCalendar.App.csproj
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    PrototypeDay.cs
    Desktop/
      DesktopAttachResult.cs
      DesktopHostService.cs
      IDesktopWindowApi.cs
      Win32DesktopWindowApi.cs
    Diagnostics/
      PrototypeReadySignal.cs
    Windowing/
      IMonitorWorkAreaReader.cs
      MonitorWorkArea.cs
      WindowPlacement.cs
      WindowPlacementCoordinator.cs
      WindowPlacementService.cs
      Win32MonitorWorkAreaReader.cs
tests/
  TrainingDeskCalendar.App.Tests/
    TrainingDeskCalendar.App.Tests.csproj
    Desktop/
      DesktopHostServiceTests.cs
    Windowing/
      WindowPlacementServiceTests.cs
scripts/
  measure-prototype.ps1
docs/
  validation/
    desktop-prototype-results.md
```

### File Responsibilities

- `DesktopHostService.cs` owns attach/fallback state decisions and contains no P/Invoke.
- `Win32DesktopWindowApi.cs` contains all desktop-host P/Invoke calls and top-level style restoration.
- `WindowPlacementService.cs` contains pure monitor and rectangle normalization logic.
- `Win32MonitorWorkAreaReader.cs` translates Windows monitor work areas into device-independent coordinates.
- `WindowPlacementCoordinator.cs` keeps the WPF window visible while monitor topology changes.
- `MainWindow.xaml` is only the visual sample required to exercise transparency, resizing, and desktop attachment.
- `PrototypeReadySignal.cs` exposes deterministic readiness and timed-exit hooks for measurement.
- `measure-prototype.ps1` is the only performance collection entry point and generates the validation report from measured values.

## Task 1: Install the SDK and Scaffold the Prototype Solution

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `TrainingDeskCalendar.sln`
- Create: `src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj`
- Create: `tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj`

- [ ] **Step 1: Install the .NET 10 SDK**

Run in PowerShell:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget
```

Expected: installation succeeds without modifying the project repository.

- [ ] **Step 2: Verify the SDK is available**

Run:

```powershell
dotnet --list-sdks
```

Expected: output contains a `10.0` SDK entry.

- [ ] **Step 3: Create the solution and projects**

Run from the repository root:

```powershell
dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature
dotnet new sln --name TrainingDeskCalendar
dotnet new wpf --name TrainingDeskCalendar.App --output src/TrainingDeskCalendar.App --framework net10.0
dotnet new xunit --name TrainingDeskCalendar.App.Tests --output tests/TrainingDeskCalendar.App.Tests --framework net10.0
dotnet sln TrainingDeskCalendar.sln add src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj
dotnet sln TrainingDeskCalendar.sln add tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj
dotnet add tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj reference src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj
```

Expected: both projects are listed by `dotnet sln TrainingDeskCalendar.sln list`.

- [ ] **Step 4: Set repository-wide compiler and restore rules**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Replace `src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
    <AssemblyName>TrainingDeskCalendar.App</AssemblyName>
    <RootNamespace>TrainingDeskCalendar.App</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="TrainingDeskCalendar.App.Tests" />
  </ItemGroup>
</Project>
```

Replace `tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\TrainingDeskCalendar.App\TrainingDeskCalendar.App.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Restore and build the empty solution**

Run:

```powershell
dotnet restore TrainingDeskCalendar.sln
dotnet build TrainingDeskCalendar.sln --configuration Debug --no-restore
```

Expected: build succeeds with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 6: Commit the scaffold**

```powershell
git add global.json Directory.Build.props TrainingDeskCalendar.sln src tests
git commit -m "build: scaffold WPF desktop prototype"
```

## Task 2: Define and Test Desktop Attachment State

**Files:**
- Create: `src/TrainingDeskCalendar.App/Desktop/DesktopAttachResult.cs`
- Create: `src/TrainingDeskCalendar.App/Desktop/IDesktopWindowApi.cs`
- Create: `src/TrainingDeskCalendar.App/Desktop/DesktopHostService.cs`
- Create: `tests/TrainingDeskCalendar.App.Tests/Desktop/DesktopHostServiceTests.cs`

- [ ] **Step 1: Write failing desktop-host service tests**

Create `tests/TrainingDeskCalendar.App.Tests/Desktop/DesktopHostServiceTests.cs`:

```csharp
using TrainingDeskCalendar.App.Desktop;

namespace TrainingDeskCalendar.App.Tests.Desktop;

public sealed class DesktopHostServiceTests
{
    [Fact]
    public void Attach_ReturnsAttached_WhenNativeApiSucceeds()
    {
        var api = new FakeDesktopWindowApi(attachSucceeds: true);
        var service = new DesktopHostService(api);

        DesktopAttachResult result = service.Attach((nint)123);

        Assert.Equal(DesktopAttachStatus.Attached, result.Status);
        Assert.Null(result.FailureReason);
        Assert.Equal(1, api.AttachCalls);
        Assert.Equal(0, api.RestoreCalls);
    }

    [Fact]
    public void Attach_RestoresTopLevelWindow_WhenNativeApiFails()
    {
        var api = new FakeDesktopWindowApi(attachSucceeds: false);
        var service = new DesktopHostService(api);

        DesktopAttachResult result = service.Attach((nint)123);

        Assert.Equal(DesktopAttachStatus.Fallback, result.Status);
        Assert.Equal("WorkerW was not found.", result.FailureReason);
        Assert.Equal(1, api.AttachCalls);
        Assert.Equal(1, api.RestoreCalls);
    }

    [Fact]
    public void Attach_RejectsAZeroWindowHandle()
    {
        var api = new FakeDesktopWindowApi(attachSucceeds: true);
        var service = new DesktopHostService(api);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Attach(nint.Zero));
    }

    private sealed class FakeDesktopWindowApi(bool attachSucceeds) : IDesktopWindowApi
    {
        public int AttachCalls { get; private set; }
        public int RestoreCalls { get; private set; }

        public bool TryAttachToDesktop(nint windowHandle, out string? failureReason)
        {
            AttachCalls++;
            failureReason = attachSucceeds ? null : "WorkerW was not found.";
            return attachSucceeds;
        }

        public void RestoreAsTopLevel(nint windowHandle)
        {
            RestoreCalls++;
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run:

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter DesktopHostServiceTests
```

Expected: compilation fails because `DesktopHostService`, `DesktopAttachResult`, and `IDesktopWindowApi` do not exist.

- [ ] **Step 3: Implement the attachment state types**

Create `src/TrainingDeskCalendar.App/Desktop/DesktopAttachResult.cs`:

```csharp
namespace TrainingDeskCalendar.App.Desktop;

internal enum DesktopAttachStatus
{
    Attached,
    Fallback
}

internal sealed record DesktopAttachResult(
    DesktopAttachStatus Status,
    string? FailureReason);
```

Create `src/TrainingDeskCalendar.App/Desktop/IDesktopWindowApi.cs`:

```csharp
namespace TrainingDeskCalendar.App.Desktop;

internal interface IDesktopWindowApi
{
    bool TryAttachToDesktop(nint windowHandle, out string? failureReason);
    void RestoreAsTopLevel(nint windowHandle);
}
```

Create `src/TrainingDeskCalendar.App/Desktop/DesktopHostService.cs`:

```csharp
namespace TrainingDeskCalendar.App.Desktop;

internal sealed class DesktopHostService(IDesktopWindowApi desktopWindowApi)
{
    public DesktopAttachResult Attach(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(windowHandle));
        }

        if (desktopWindowApi.TryAttachToDesktop(windowHandle, out string? failureReason))
        {
            return new DesktopAttachResult(DesktopAttachStatus.Attached, null);
        }

        desktopWindowApi.RestoreAsTopLevel(windowHandle);
        return new DesktopAttachResult(
            DesktopAttachStatus.Fallback,
            failureReason ?? "Desktop attachment failed without a native error message.");
    }
}
```

- [ ] **Step 4: Run the service tests**

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter DesktopHostServiceTests
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit the state service**

```powershell
git add src/TrainingDeskCalendar.App/Desktop tests/TrainingDeskCalendar.App.Tests/Desktop
git commit -m "test: define desktop attachment fallback state"
```

## Task 3: Implement the Win32 Desktop Host and Visual Prototype

**Files:**
- Create: `src/TrainingDeskCalendar.App/Desktop/Win32DesktopWindowApi.cs`
- Create: `src/TrainingDeskCalendar.App/PrototypeDay.cs`
- Modify: `src/TrainingDeskCalendar.App/App.xaml`
- Modify: `src/TrainingDeskCalendar.App/App.xaml.cs`
- Modify: `src/TrainingDeskCalendar.App/MainWindow.xaml`
- Modify: `src/TrainingDeskCalendar.App/MainWindow.xaml.cs`

- [ ] **Step 1: Implement the Win32 desktop API adapter**

Create `src/TrainingDeskCalendar.App/Desktop/Win32DesktopWindowApi.cs`:

```csharp
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrainingDeskCalendar.App.Desktop;

internal sealed class Win32DesktopWindowApi : IDesktopWindowApi
{
    private const uint SpawnWorkerMessage = 0x052C;
    private const uint SmtoNormal = 0x0000;
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000L;
    private const long WsPopup = unchecked((long)0x80000000L);
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;

    private readonly ConcurrentDictionary<nint, nint> originalStyles = new();

    public bool TryAttachToDesktop(nint windowHandle, out string? failureReason)
    {
        nint workerWindow = FindDesktopWorkerWindow();
        if (workerWindow == nint.Zero)
        {
            failureReason = "WorkerW was not found.";
            return false;
        }

        if (GetParent(windowHandle) == workerWindow)
        {
            failureReason = null;
            return true;
        }

        nint originalStyle = originalStyles.GetOrAdd(
            windowHandle,
            handle => GetWindowLongPtr(handle, GwlStyle));
        nint childStyle = (nint)(((long)originalStyle & ~WsPopup) | WsChild);
        _ = SetWindowLongPtr(windowHandle, GwlStyle, childStyle);

        Marshal.SetLastPInvokeError(0);
        nint previousParent = SetParent(windowHandle, workerWindow);
        int error = Marshal.GetLastPInvokeError();
        if (previousParent == nint.Zero && error != 0)
        {
            _ = SetWindowLongPtr(windowHandle, GwlStyle, originalStyle);
            originalStyles.TryRemove(windowHandle, out _);
            failureReason = new Win32Exception(error).Message;
            return false;
        }

        _ = SetWindowPos(
            windowHandle,
            nint.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);

        failureReason = null;
        return true;
    }

    public void RestoreAsTopLevel(nint windowHandle)
    {
        _ = SetParent(windowHandle, nint.Zero);

        if (originalStyles.TryRemove(windowHandle, out nint originalStyle))
        {
            _ = SetWindowLongPtr(windowHandle, GwlStyle, originalStyle);
        }

        _ = SetWindowPos(
            windowHandle,
            nint.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
    }

    private static nint FindDesktopWorkerWindow()
    {
        nint programManager = FindWindow("Progman", null);
        if (programManager == nint.Zero)
        {
            return nint.Zero;
        }

        _ = SendMessageTimeout(
            programManager,
            SpawnWorkerMessage,
            (nint)0xD,
            (nint)0x1,
            SmtoNormal,
            1_000,
            out _);

        nint workerWindow = nint.Zero;
        _ = EnumWindows((topLevelWindow, _) =>
        {
            nint shellView = FindWindowEx(topLevelWindow, nint.Zero, "SHELLDLL_DefView", null);
            if (shellView == nint.Zero)
            {
                return true;
            }

            workerWindow = FindWindowEx(nint.Zero, topLevelWindow, "WorkerW", null);
            return false;
        }, nint.Zero);

        return workerWindow;
    }

    private delegate bool EnumWindowsCallback(nint windowHandle, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindowEx(
        nint parentWindow,
        nint childAfter,
        string? className,
        string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeoutMilliseconds,
        out nint result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint childWindow, nint newParentWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetParent(nint windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
```

- [ ] **Step 2: Add the visual sample model**

Create `src/TrainingDeskCalendar.App/PrototypeDay.cs`:

```csharp
using System.Windows.Media;

namespace TrainingDeskCalendar.App;

public sealed record PrototypeDay(
    string Weekday,
    int DayNumber,
    string Plan,
    Brush Background,
    bool IsToday);
```

- [ ] **Step 3: Replace the application startup markup**

Replace `src/TrainingDeskCalendar.App/App.xaml` with:

```xml
<Application x:Class="TrainingDeskCalendar.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources />
</Application>
```

Replace `src/TrainingDeskCalendar.App/App.xaml.cs` with:

```csharp
using System.Windows;

namespace TrainingDeskCalendar.App;

public partial class App : Application
{
    internal static string? ReadyFilePath { get; private set; }
    internal static TimeSpan? ExitAfter { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        for (int index = 0; index < e.Args.Length; index++)
        {
            if (e.Args[index] == "--ready-file" && index + 1 < e.Args.Length)
            {
                ReadyFilePath = e.Args[++index];
            }
            else if (e.Args[index] == "--exit-after-seconds" &&
                     index + 1 < e.Args.Length &&
                     int.TryParse(e.Args[++index], out int seconds))
            {
                ExitAfter = TimeSpan.FromSeconds(seconds);
            }
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
```

- [ ] **Step 4: Build the transparent two-week sample window**

Replace `src/TrainingDeskCalendar.App/MainWindow.xaml` with:

```xml
<Window x:Class="TrainingDeskCalendar.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="训练桌历桌面原型"
        Width="1120"
        Height="470"
        MinWidth="840"
        MinHeight="360"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ResizeMode="CanResizeWithGrip"
        ShowInTaskbar="False">
  <Border Margin="10"
          Padding="14"
          CornerRadius="8"
          Background="#D1F7F9FA"
          BorderBrush="#BFFFFFFF"
          BorderThickness="1">
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
        <RowDefinition Height="Auto" />
      </Grid.RowDefinitions>

      <DockPanel Margin="0,0,0,10"
                 MouseLeftButtonDown="OnHeaderMouseLeftButtonDown">
        <TextBlock DockPanel.Dock="Left"
                   FontSize="18"
                   FontWeight="SemiBold"
                   Text="训练桌历 · Windows 原型" />
        <TextBlock DockPanel.Dock="Right"
                   VerticalAlignment="Center"
                   Foreground="#5D6972"
                   Text="本周 + 下周" />
      </DockPanel>

      <ItemsControl Grid.Row="1" ItemsSource="{Binding Days}">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <UniformGrid Columns="7" Rows="2" />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Margin="3"
                    Padding="8"
                    CornerRadius="5"
                    BorderBrush="#28000000"
                    BorderThickness="1"
                    Background="{Binding Background}">
              <Grid>
                <Grid.RowDefinitions>
                  <RowDefinition Height="Auto" />
                  <RowDefinition Height="*" />
                </Grid.RowDefinitions>
                <DockPanel>
                  <TextBlock FontSize="11" Foreground="#5F6B74" Text="{Binding Weekday}" />
                  <TextBlock DockPanel.Dock="Right"
                             FontSize="16"
                             FontWeight="Bold"
                             Text="{Binding DayNumber}" />
                </DockPanel>
                <TextBlock Grid.Row="1"
                           Margin="0,8,0,0"
                           FontSize="12"
                           TextWrapping="Wrap"
                           Text="{Binding Plan}" />
              </Grid>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>

      <TextBlock x:Name="DesktopStatusText"
                 Grid.Row="2"
                 Margin="0,8,0,0"
                 HorizontalAlignment="Right"
                 FontSize="11"
                 Foreground="#5D6972" />
    </Grid>
  </Border>
</Window>
```

- [ ] **Step 5: Connect the WPF window to the desktop host and Explorer restart message**

Replace `src/TrainingDeskCalendar.App/MainWindow.xaml.cs` with:

```csharp
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingDeskCalendar.App.Desktop;
using TrainingDeskCalendar.App.Diagnostics;

namespace TrainingDeskCalendar.App;

public partial class MainWindow : Window
{
    private readonly DesktopHostService desktopHostService =
        new(new Win32DesktopWindowApi());
    private readonly DispatcherTimer desktopWatchdog;
    private readonly uint taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
    private nint windowHandle;

    public MainWindow()
    {
        InitializeComponent();
        Days = CreateDays();
        DataContext = this;
        desktopWatchdog = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        desktopWatchdog.Tick += (_, _) => AttachToDesktop();
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Closed += (_, _) => desktopWatchdog.Stop();
    }

    public ObservableCollection<PrototypeDay> Days { get; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        windowHandle = helper.Handle;
        HwndSource.FromHwnd(windowHandle)?.AddHook(WindowMessageHook);
        AttachToDesktop();
        desktopWatchdog.Start();
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        PrototypeReadySignal.Write(App.ReadyFilePath);

        if (App.ExitAfter is TimeSpan delay)
        {
            var timer = new DispatcherTimer { Interval = delay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Application.Current.Shutdown();
            };
            timer.Start();
        }
    }

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if ((uint)message == taskbarCreatedMessage)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(AttachToDesktop));
        }

        return nint.Zero;
    }

    private void AttachToDesktop()
    {
        DesktopAttachResult result = desktopHostService.Attach(windowHandle);
        DesktopStatusText.Text = result.Status == DesktopAttachStatus.Attached
            ? "Desktop host: attached"
            : $"Desktop host: fallback · {result.FailureReason}";
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private static ObservableCollection<PrototypeDay> CreateDays()
    {
        string[] colors =
        [
            "#BFE3DA", "#D5DADF", "#C7D8F2", "#BFE3DA", "#D9C7E8", "#F1C2C2", "#D5DADF",
            "#BFE3DA", "#D5DADF", "#C7D8F2", "#BFE3DA", "#F4D1A6", "#D9C7E8", "#D5DADF"
        ];
        string[] plans =
        [
            "胸部训练\n卧推 4 × 8", "恢复日\n步行 30 分钟", "慢跑 5 km", "背部训练",
            "核心训练", "腿部训练", "完全休息", "肩部训练", "泡沫轴", "间歇跑",
            "硬拉 4 × 6", "灵活性训练", "全身循环", "完全休息"
        ];
        string[] weekdays = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];

        return new ObservableCollection<PrototypeDay>(
            Enumerable.Range(0, 14).Select(index => new PrototypeDay(
                weekdays[index % 7],
                17 + index,
                plans[index],
                (Brush)new BrushConverter().ConvertFromString(colors[index])!,
                index == 0)));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string messageName);
}
```

- [ ] **Step 6: Add deterministic readiness signaling**

Create `src/TrainingDeskCalendar.App/Diagnostics/PrototypeReadySignal.cs`:

```csharp
namespace TrainingDeskCalendar.App.Diagnostics;

internal static class PrototypeReadySignal
{
    public static void Write(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, DateTimeOffset.UtcNow.ToString("O"));
    }
}
```

- [ ] **Step 7: Build and launch the prototype**

Run:

```powershell
dotnet build TrainingDeskCalendar.sln --configuration Debug
dotnet run --project src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj
```

Expected: a transparent 14-day widget appears on the desktop, has no taskbar button, and displays either `Desktop host: attached` or an explicit fallback reason.

- [ ] **Step 8: Commit the native prototype**

```powershell
git add src/TrainingDeskCalendar.App
git commit -m "feat: attach WPF prototype to Windows desktop host"
```

## Task 4: Test and Implement Multi-Monitor Placement Normalization

**Files:**
- Create: `src/TrainingDeskCalendar.App/Windowing/IMonitorWorkAreaReader.cs`
- Create: `src/TrainingDeskCalendar.App/Windowing/MonitorWorkArea.cs`
- Create: `src/TrainingDeskCalendar.App/Windowing/WindowPlacement.cs`
- Create: `src/TrainingDeskCalendar.App/Windowing/Win32MonitorWorkAreaReader.cs`
- Create: `src/TrainingDeskCalendar.App/Windowing/WindowPlacementCoordinator.cs`
- Create: `src/TrainingDeskCalendar.App/Windowing/WindowPlacementService.cs`
- Modify: `src/TrainingDeskCalendar.App/MainWindow.xaml.cs`
- Create: `tests/TrainingDeskCalendar.App.Tests/Windowing/WindowPlacementServiceTests.cs`

- [ ] **Step 1: Write failing placement tests**

Create `tests/TrainingDeskCalendar.App.Tests/Windowing/WindowPlacementServiceTests.cs`:

```csharp
using TrainingDeskCalendar.App.Windowing;

namespace TrainingDeskCalendar.App.Tests.Windowing;

public sealed class WindowPlacementServiceTests
{
    private static readonly MonitorWorkArea Primary =
        new("primary", 0, 0, 1920, 1040, isPrimary: true);

    [Fact]
    public void Normalize_PreservesAVisiblePlacementOnTheSavedMonitor()
    {
        var service = new WindowPlacementService();
        var saved = new WindowPlacement("primary", 100, 120, 1120, 470);

        WindowPlacement result = service.Normalize(saved, [Primary]);

        Assert.Equal(saved, result);
    }

    [Fact]
    public void Normalize_MovesToPrimaryMonitorWhenSavedMonitorIsMissing()
    {
        var service = new WindowPlacementService();
        var saved = new WindowPlacement("removed", 2600, 100, 1120, 470);

        WindowPlacement result = service.Normalize(saved, [Primary]);

        Assert.Equal("primary", result.MonitorId);
        Assert.Equal(400, result.X);
        Assert.Equal(285, result.Y);
    }

    [Fact]
    public void Normalize_ClampsSizeAndKeepsNinetySixPixelsVisible()
    {
        var service = new WindowPlacementService();
        var saved = new WindowPlacement("primary", 1900, 1020, 400, 200);

        WindowPlacement result = service.Normalize(saved, [Primary]);

        Assert.Equal(840, result.Width);
        Assert.Equal(360, result.Height);
        Assert.True(result.X <= Primary.Right - 96);
        Assert.True(result.Y <= Primary.Bottom - 96);
    }
}
```

- [ ] **Step 2: Run the placement tests and confirm they fail**

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter WindowPlacementServiceTests
```

Expected: compilation fails because the placement types do not exist.

- [ ] **Step 3: Implement the placement value types**

Create `src/TrainingDeskCalendar.App/Windowing/MonitorWorkArea.cs`:

```csharp
namespace TrainingDeskCalendar.App.Windowing;

internal sealed record MonitorWorkArea(
    string Id,
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}
```

Create `src/TrainingDeskCalendar.App/Windowing/WindowPlacement.cs`:

```csharp
namespace TrainingDeskCalendar.App.Windowing;

internal sealed record WindowPlacement(
    string MonitorId,
    double X,
    double Y,
    double Width,
    double Height);
```

- [ ] **Step 4: Implement placement normalization**

Create `src/TrainingDeskCalendar.App/Windowing/WindowPlacementService.cs`:

```csharp
namespace TrainingDeskCalendar.App.Windowing;

internal sealed class WindowPlacementService
{
    private const double MinimumWidth = 840;
    private const double MinimumHeight = 360;
    private const double MinimumVisible = 96;

    public WindowPlacement Normalize(
        WindowPlacement saved,
        IReadOnlyCollection<MonitorWorkArea> monitors)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(monitors);

        MonitorWorkArea? savedMonitor = monitors.FirstOrDefault(
            monitor => monitor.Id == saved.MonitorId);
        MonitorWorkArea target = savedMonitor
            ?? monitors.FirstOrDefault(monitor => monitor.IsPrimary)
            ?? throw new InvalidOperationException("No monitor work area is available.");

        double effectiveMinimumWidth = Math.Min(MinimumWidth, target.Width);
        double effectiveMinimumHeight = Math.Min(MinimumHeight, target.Height);
        double width = Math.Clamp(saved.Width, effectiveMinimumWidth, target.Width);
        double height = Math.Clamp(saved.Height, effectiveMinimumHeight, target.Height);
        double minimumX = target.Left - width + MinimumVisible;
        double maximumX = target.Right - MinimumVisible;
        double minimumY = target.Top;
        double maximumY = target.Bottom - MinimumVisible;
        double requestedX = savedMonitor is null
            ? target.Left + ((target.Width - width) / 2)
            : saved.X;
        double requestedY = savedMonitor is null
            ? target.Top + ((target.Height - height) / 2)
            : saved.Y;

        return new WindowPlacement(
            target.Id,
            Math.Clamp(requestedX, minimumX, maximumX),
            Math.Clamp(requestedY, minimumY, maximumY),
            width,
            height);
    }
}
```

- [ ] **Step 5: Run the complete test suite**

```powershell
dotnet test TrainingDeskCalendar.sln --configuration Debug
```

Expected: all 6 tests pass.

- [ ] **Step 6: Define the monitor topology reader boundary**

Create `src/TrainingDeskCalendar.App/Windowing/IMonitorWorkAreaReader.cs`:

```csharp
namespace TrainingDeskCalendar.App.Windowing;

internal interface IMonitorWorkAreaReader
{
    IReadOnlyList<MonitorWorkArea> GetAll();
    string GetMonitorIdForWindow(nint windowHandle);
}
```

- [ ] **Step 7: Implement the Win32 monitor reader**

Create `src/TrainingDeskCalendar.App/Windowing/Win32MonitorWorkAreaReader.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrainingDeskCalendar.App.Windowing;

internal sealed class Win32MonitorWorkAreaReader : IMonitorWorkAreaReader
{
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const uint MonitorInfoPrimary = 0x00000001;
    private const int EffectiveDpi = 0;

    public IReadOnlyList<MonitorWorkArea> GetAll()
    {
        var monitors = new List<MonitorWorkArea>();
        bool succeeded = EnumDisplayMonitors(
            nint.Zero,
            nint.Zero,
            (monitorHandle, _, _, _) =>
            {
                monitors.Add(ReadMonitor(monitorHandle));
                return true;
            },
            nint.Zero);

        if (!succeeded)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return monitors;
    }

    public string GetMonitorIdForWindow(nint windowHandle)
    {
        nint monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        return ReadMonitor(monitorHandle).Id;
    }

    private static MonitorWorkArea ReadMonitor(nint monitorHandle)
    {
        var info = new MonitorInfoEx
        {
            Size = Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };

        if (!GetMonitorInfo(monitorHandle, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        uint dpiX = 96;
        uint dpiY = 96;
        _ = GetDpiForMonitor(monitorHandle, EffectiveDpi, out dpiX, out dpiY);
        double scale = 96d / Math.Max(dpiX, 1);

        return new MonitorWorkArea(
            info.DeviceName,
            info.WorkArea.Left * scale,
            info.WorkArea.Top * scale,
            info.WorkArea.Right * scale - info.WorkArea.Left * scale,
            info.WorkArea.Bottom * scale - info.WorkArea.Top * scale,
            (info.Flags & MonitorInfoPrimary) != 0);
    }

    private delegate bool MonitorEnumCallback(
        nint monitorHandle,
        nint deviceContext,
        nint monitorRectangle,
        nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRectangle MonitorArea;
        public NativeRectangle WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRectangle,
        MonitorEnumCallback callback,
        nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitorHandle, ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitorHandle,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
```

- [ ] **Step 8: Coordinate the WPF window with topology changes**

Create `src/TrainingDeskCalendar.App/Windowing/WindowPlacementCoordinator.cs`:

```csharp
using System.Windows;

namespace TrainingDeskCalendar.App.Windowing;

internal sealed class WindowPlacementCoordinator(
    Window window,
    nint windowHandle,
    IMonitorWorkAreaReader monitorReader,
    WindowPlacementService placementService)
{
    private string lastKnownMonitorId = monitorReader.GetMonitorIdForWindow(windowHandle);
    private bool applyingPlacement;

    public void TrackCurrentMonitor()
    {
        if (applyingPlacement)
        {
            return;
        }

        lastKnownMonitorId = monitorReader.GetMonitorIdForWindow(windowHandle);
    }

    public void EnsureVisible()
    {
        IReadOnlyList<MonitorWorkArea> monitors = monitorReader.GetAll();
        var current = new WindowPlacement(
            lastKnownMonitorId,
            window.Left,
            window.Top,
            window.ActualWidth,
            window.ActualHeight);
        WindowPlacement normalized = placementService.Normalize(current, monitors);

        if (ApproximatelyEqual(current, normalized))
        {
            lastKnownMonitorId = normalized.MonitorId;
            return;
        }

        applyingPlacement = true;
        try
        {
            window.Left = normalized.X;
            window.Top = normalized.Y;
            window.Width = normalized.Width;
            window.Height = normalized.Height;
            lastKnownMonitorId = normalized.MonitorId;
        }
        finally
        {
            applyingPlacement = false;
        }
    }

    private static bool ApproximatelyEqual(WindowPlacement left, WindowPlacement right)
    {
        return left.MonitorId == right.MonitorId &&
               Math.Abs(left.X - right.X) < 0.5 &&
               Math.Abs(left.Y - right.Y) < 0.5 &&
               Math.Abs(left.Width - right.Width) < 0.5 &&
               Math.Abs(left.Height - right.Height) < 0.5;
    }
}
```

- [ ] **Step 9: Connect placement recovery to the five-second watchdog**

In `MainWindow.xaml.cs`, add:

```csharp
using TrainingDeskCalendar.App.Windowing;
```

Add this field:

```csharp
private WindowPlacementCoordinator? placementCoordinator;
```

Replace the watchdog subscription in the constructor:

```csharp
desktopWatchdog.Tick += OnDesktopWatchdogTick;
LocationChanged += (_, _) => placementCoordinator?.TrackCurrentMonitor();
```

Add this line to `OnSourceInitialized` after assigning `windowHandle`:

```csharp
placementCoordinator = new WindowPlacementCoordinator(
    this,
    windowHandle,
    new Win32MonitorWorkAreaReader(),
    new WindowPlacementService());
```

Add this method:

```csharp
private void OnDesktopWatchdogTick(object? sender, EventArgs e)
{
    AttachToDesktop();
    placementCoordinator?.EnsureVisible();
}
```

- [ ] **Step 10: Build and run all tests**

```powershell
dotnet build TrainingDeskCalendar.sln --configuration Debug
dotnet test TrainingDeskCalendar.sln --configuration Debug --no-build
```

Expected: build succeeds and all 6 tests pass.

- [ ] **Step 11: Commit placement normalization and topology recovery**

```powershell
git add src/TrainingDeskCalendar.App/Windowing tests/TrainingDeskCalendar.App.Tests/Windowing
git add src/TrainingDeskCalendar.App/MainWindow.xaml.cs
git commit -m "feat: recover desktop placement after display changes"
```

## Task 5: Add Repeatable Publish and Performance Measurement

**Files:**
- Create: `scripts/measure-prototype.ps1`
- Create during execution: `artifacts/prototype/results.json`
- Create during execution: `docs/validation/desktop-prototype-results.md`

- [ ] **Step 1: Create the measurement script**

Create `scripts/measure-prototype.ps1`:

```powershell
[CmdletBinding()]
param(
    [int]$Runs = 5,
    [int]$IdleSeconds = 15
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\TrainingDeskCalendar.App\TrainingDeskCalendar.App.csproj'
$artifactRoot = Join-Path $repoRoot 'artifacts\prototype'
$frameworkDir = Join-Path $artifactRoot 'framework-dependent'
$selfContainedDir = Join-Path $artifactRoot 'self-contained'
$resultPath = Join-Path $artifactRoot 'results.json'
$reportPath = Join-Path $repoRoot 'docs\validation\desktop-prototype-results.md'

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $reportPath) | Out-Null

dotnet publish $project --configuration Release --runtime win-x64 --self-contained false --output $frameworkDir
if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publish failed.' }

dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --output $selfContainedDir
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }

function Get-DirectorySize([string]$Path) {
    return (Get-ChildItem -LiteralPath $Path -File -Recurse | Measure-Object Length -Sum).Sum
}

function Measure-Run([string]$Executable, [int]$RunNumber) {
    $readyFile = Join-Path $artifactRoot "ready-$RunNumber.txt"
    Remove-Item -LiteralPath $readyFile -Force -ErrorAction SilentlyContinue

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $Executable -ArgumentList @(
        '--ready-file', $readyFile,
        '--exit-after-seconds', ($IdleSeconds + 15)
    ) -PassThru

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $readyFile)) {
        if ($process.HasExited) { throw "Prototype exited before ready signal on run $RunNumber." }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Ready timeout on run $RunNumber." }
        Start-Sleep -Milliseconds 25
        $process.Refresh()
    }
    $stopwatch.Stop()

    Start-Sleep -Seconds 2
    $process.Refresh()
    $cpuStart = $process.TotalProcessorTime
    Start-Sleep -Seconds $IdleSeconds
    $process.Refresh()
    $cpuEnd = $process.TotalProcessorTime
    $cpuPercent = (($cpuEnd - $cpuStart).TotalSeconds / ($IdleSeconds * [Environment]::ProcessorCount)) * 100

    $measurement = [ordered]@{
        run = $RunNumber
        startupMilliseconds = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 1)
        workingSetBytes = $process.WorkingSet64
        idleCpuPercent = [Math]::Round($cpuPercent, 3)
    }

    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }

    return [pscustomobject]$measurement
}

$executable = Join-Path $selfContainedDir 'TrainingDeskCalendar.App.exe'
$measurements = 1..$Runs | ForEach-Object { Measure-Run -Executable $executable -RunNumber $_ }

$frameworkZip = Join-Path $artifactRoot 'framework-dependent.zip'
$selfContainedZip = Join-Path $artifactRoot 'self-contained.zip'
Remove-Item -LiteralPath $frameworkZip, $selfContainedZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $frameworkDir '*') -DestinationPath $frameworkZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $selfContainedDir '*') -DestinationPath $selfContainedZip -CompressionLevel Optimal

$summary = [ordered]@{
    measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    runs = $Runs
    averageStartupMilliseconds = [Math]::Round(($measurements.startupMilliseconds | Measure-Object -Average).Average, 1)
    maximumWorkingSetBytes = ($measurements.workingSetBytes | Measure-Object -Maximum).Maximum
    averageIdleCpuPercent = [Math]::Round(($measurements.idleCpuPercent | Measure-Object -Average).Average, 3)
    frameworkDependentDirectoryBytes = Get-DirectorySize $frameworkDir
    selfContainedDirectoryBytes = Get-DirectorySize $selfContainedDir
    frameworkDependentZipBytes = (Get-Item -LiteralPath $frameworkZip).Length
    selfContainedZipBytes = (Get-Item -LiteralPath $selfContainedZip).Length
    measurements = $measurements
}

$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding utf8

$memoryMb = [Math]::Round($summary.maximumWorkingSetBytes / 1MB, 1)
$frameworkDirectoryMb = [Math]::Round($summary.frameworkDependentDirectoryBytes / 1MB, 1)
$selfContainedDirectoryMb = [Math]::Round($summary.selfContainedDirectoryBytes / 1MB, 1)
$frameworkZipMb = [Math]::Round($summary.frameworkDependentZipBytes / 1MB, 1)
$selfContainedZipMb = [Math]::Round($summary.selfContainedZipBytes / 1MB, 1)

$startupPass = $summary.averageStartupMilliseconds -le 2000
$memoryPass = $memoryMb -le 100
$cpuPass = $summary.averageIdleCpuPercent -lt 0.5
$selfContainedSizePass = $selfContainedDirectoryMb -le 150 -and $selfContainedZipMb -le 80
$packagingDecision = if ($selfContainedSizePass) {
    'Self-contained win-x64 publish satisfies the prototype size gate.'
} else {
    'Use a framework-dependent app with a per-user .NET Desktop Runtime bootstrapper; validate final installer size in phase 3.'
}

$report = @"
# Desktop Prototype Validation Results

- Measured UTC: $($summary.measuredAtUtc)
- Runs: $Runs
- Average cold startup: $($summary.averageStartupMilliseconds) ms — $(if ($startupPass) { 'PASS' } else { 'FAIL' })
- Maximum working set: $memoryMb MB — $(if ($memoryPass) { 'PASS' } else { 'FAIL' })
- Average idle CPU: $($summary.averageIdleCpuPercent)% — $(if ($cpuPass) { 'PASS' } else { 'FAIL' })
- Framework-dependent directory: $frameworkDirectoryMb MB
- Framework-dependent ZIP: $frameworkZipMb MB
- Self-contained directory: $selfContainedDirectoryMb MB
- Self-contained ZIP: $selfContainedZipMb MB

## Packaging Decision

$packagingDecision

## Automated Gate

Overall automated result: $(if ($startupPass -and $memoryPass -and $cpuPass) { 'PASS' } else { 'FAIL' })
"@

$report | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Host $report

if (-not ($startupPass -and $memoryPass -and $cpuPass)) {
    exit 1
}
```

- [ ] **Step 2: Run all tests before measurement**

```powershell
dotnet test TrainingDeskCalendar.sln --configuration Release
```

Expected: all tests pass.

- [ ] **Step 3: Run the measurement harness**

```powershell
pwsh -File scripts/measure-prototype.ps1 -Runs 5 -IdleSeconds 15
```

Expected: the script creates `artifacts/prototype/results.json` and `docs/validation/desktop-prototype-results.md`. It exits with code 0 only when startup, memory, and idle CPU gates pass.

- [ ] **Step 4: Ensure generated binaries remain untracked**

Append these entries to `.gitignore`:

```gitignore
artifacts/
**/bin/
**/obj/
```

Run:

```powershell
git status --short
```

Expected: `artifacts/`, `bin/`, and `obj/` do not appear; the measurement script and generated Markdown report do appear.

- [ ] **Step 5: Commit the measurement harness and measured report**

```powershell
git add .gitignore scripts/measure-prototype.ps1 docs/validation/desktop-prototype-results.md
git commit -m "test: measure desktop prototype performance"
```

## Task 6: Complete Windows Behavior Validation and Gate the Next Phase

**Files:**
- Modify: `docs/validation/desktop-prototype-results.md`
- Create: `docs/validation/desktop-prototype-manual-checks.md`

- [ ] **Step 1: Create the manual validation record**

Create `docs/validation/desktop-prototype-manual-checks.md` with this exact checklist:

```markdown
# Desktop Prototype Manual Checks

## Required Environments

- Windows 10 22H2 x64, 100% DPI
- Windows 11 24H2 x64, 150% DPI
- Latest stable Windows 11 x64 available at validation time

## Desktop Behavior

- [ ] Widget has no taskbar button.
- [ ] Widget is visible on the desktop after startup.
- [ ] An ordinary application window covers the widget.
- [ ] Widget never becomes always-on-top.
- [ ] Win+D leaves the widget in a usable desktop state.
- [ ] Restarting Windows Explorer reconnects the widget within 5 seconds.
- [ ] Forced desktop-host failure shows a normal non-topmost fallback window.

## Window and Display Behavior

- [ ] Widget can move and resize at 100% DPI.
- [ ] Widget can move and resize at 150% DPI.
- [ ] Moving the widget to a second monitor keeps it visible.
- [ ] Disconnecting the saved monitor moves the widget to the primary work area.
- [ ] Sleep and resume leave the widget visible and interactive.

## Gate Decision

- [ ] All automated performance gates pass.
- [ ] All required manual checks pass on every required environment.
- [ ] The packaging decision is recorded in `desktop-prototype-results.md`.
- [ ] WPF and the selected packaging mode are approved for phase 1.
```

- [ ] **Step 2: Validate normal desktop behavior**

Run the Release prototype:

```powershell
dotnet run --project src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj --configuration Release
```

Perform every Desktop Behavior and Window and Display Behavior check. Mark a checkbox only after observing the result on the named environment.

Expected: every required checkbox becomes checked. Any failed checkbox blocks phase 1 and requires a prototype fix plus a new measurement run.

- [ ] **Step 3: Validate Explorer restart recovery**

With the prototype running, restart Explorer from Windows Task Manager using the `Restart` command for `Windows Explorer`.

Expected: the prototype process remains alive and reports `Desktop host: attached` again within 5 seconds. Do not terminate Explorer with a shell script because that would bypass the user-visible recovery scenario being tested.

- [ ] **Step 4: Validate fallback explicitly**

Temporarily change `FindDesktopWorkerWindow()` in `Win32DesktopWindowApi.cs` to return `nint.Zero`, run the prototype, and verify the visible fallback behavior. Revert only this temporary line after the check.

Run after reverting:

```powershell
git diff -- src/TrainingDeskCalendar.App/Desktop/Win32DesktopWindowApi.cs
```

Expected: no diff remains, and the manual checklist records the fallback check as passed.

- [ ] **Step 5: Re-run the automated gate after manual validation**

```powershell
dotnet test TrainingDeskCalendar.sln --configuration Release
pwsh -File scripts/measure-prototype.ps1 -Runs 5 -IdleSeconds 15
```

Expected: tests pass and measurement script exits 0 with an overall automated result of `PASS`.

- [ ] **Step 6: Record the phase gate decision**

Append this section to `docs/validation/desktop-prototype-results.md`, using the packaging decision already generated by the script:

```markdown
## Phase Gate Decision

The Windows desktop-host behavior, fallback path, display recovery, and automated performance gates passed on the required test matrix. Phase 1 may begin using WPF on .NET 10 with the packaging mode stated above.
```

Do not add this section if any required check is still failing.

- [ ] **Step 7: Commit the approved prototype gate**

```powershell
git add docs/validation/desktop-prototype-manual-checks.md docs/validation/desktop-prototype-results.md
git commit -m "docs: approve Windows prototype gate"
```

- [ ] **Step 8: Verify the branch is ready for the phase 1 plan**

```powershell
git status --short --branch
git log --oneline -6
```

Expected: working tree is clean and the latest commits cover scaffold, desktop state, native prototype, placement tests, measurements, and gate approval.

## Plan Completion Condition

After Task 6 passes, stop implementation and write the separate phase 1 plan for core domain and local data. Do not start production feature code from the roadmap alone.
