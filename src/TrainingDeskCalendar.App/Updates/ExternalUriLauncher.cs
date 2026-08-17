using System.Diagnostics;

namespace TrainingDeskCalendar.App.Updates;

internal sealed class ExternalUriLauncher : IExternalUriLauncher
{
    private readonly Action<ProcessStartInfo> start;

    public ExternalUriLauncher(Action<ProcessStartInfo>? start = null)
    {
        this.start = start ?? (startInfo => Process.Start(startInfo));
    }

    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Only absolute HTTPS URIs can be opened.", nameof(uri));
        }

        start(new ProcessStartInfo(uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
    }
}
