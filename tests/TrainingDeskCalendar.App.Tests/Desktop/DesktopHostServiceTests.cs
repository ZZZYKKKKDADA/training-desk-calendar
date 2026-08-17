using TrainingDeskCalendar.App.Desktop;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Desktop;

public sealed class DesktopHostServiceTests
{
    [Fact]
    public void Attach_WhenNativeAttachmentSucceeds_ReturnsAttached()
    {
        var api = new FakeDesktopWindowApi(attachSucceeds: true);
        var service = new DesktopHostService(api);

        DesktopAttachResult result = service.Attach((nint)123);

        Assert.Equal(DesktopAttachStatus.Attached, result.Status);
        Assert.Null(result.FailureReason);
        Assert.Equal(1, api.AttachCallCount);
        Assert.Equal(0, api.RestoreCallCount);
    }

    [Fact]
    public void Attach_WhenNativeAttachmentFails_RestoresWindowAndReturnsFallback()
    {
        const string failureReason = "WorkerW was not found.";
        var api = new FakeDesktopWindowApi(attachSucceeds: false, failureReason);
        var service = new DesktopHostService(api);

        DesktopAttachResult result = service.Attach((nint)123);

        Assert.Equal(DesktopAttachStatus.Fallback, result.Status);
        Assert.Equal(failureReason, result.FailureReason);
        Assert.Equal(1, api.AttachCallCount);
        Assert.Equal(1, api.RestoreCallCount);
    }

    [Fact]
    public void Attach_WhenWindowHandleIsZero_ThrowsArgumentOutOfRangeException()
    {
        var api = new FakeDesktopWindowApi(attachSucceeds: true);
        var service = new DesktopHostService(api);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Attach(nint.Zero));
    }

    private sealed class FakeDesktopWindowApi(
        bool attachSucceeds,
        string? failureReason = null) : IDesktopWindowApi
    {
        public int AttachCallCount { get; private set; }

        public int RestoreCallCount { get; private set; }

        public bool TryAttachToDesktop(nint windowHandle, out string? nativeFailureReason)
        {
            AttachCallCount++;
            nativeFailureReason = failureReason;
            return attachSucceeds;
        }

        public void RestoreAsTopLevel(nint windowHandle)
        {
            RestoreCallCount++;
        }
    }
}
