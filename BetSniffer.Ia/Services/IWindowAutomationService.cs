using BetSniffer.Ia.Models;

namespace BetSniffer.Ia.Services;

public interface IWindowAutomationService
{
    Task<WindowHandle> OpenBrowserAsync(string url, CancellationToken cancellationToken);
    Task<string> CaptureScreenshotBase64Async(WindowHandle handle, CancellationToken cancellationToken);
    Task ExecuteActionAsync(WindowHandle handle, VisualAgentResponse response, CancellationToken cancellationToken);
    Task CloseWindowAsync(WindowHandle handle, CancellationToken cancellationToken);
}

public readonly record struct WindowHandle(int Id, string Title);
