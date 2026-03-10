using BetSniffer.Ia.Models;

namespace BetSniffer.Ia.Services;

public sealed class WindowsInputAutomationService : IWindowAutomationService
{
    public Task<WindowHandle> OpenBrowserAsync(string url, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsInputAutomationService requer Windows para controlar janelas por input nativo.");
        }

        // Skeleton: trocar por Process.Start + Win32 (SetForegroundWindow, SendInput, PrintWindow, etc.).
        var pid = Random.Shared.Next(1000, 9999);
        return Task.FromResult(new WindowHandle(pid, $"Browser-{pid}"));
    }

    public Task<string> CaptureScreenshotBase64Async(WindowHandle handle, CancellationToken cancellationToken)
    {
        // Skeleton: substituir por captura da janela alvo (evitar screen inteira).
        var placeholder = Convert.ToBase64String("placeholder-image"u8.ToArray());
        return Task.FromResult(placeholder);
    }

    public Task ExecuteActionAsync(WindowHandle handle, VisualAgentResponse response, CancellationToken cancellationToken)
    {
        // Skeleton: mapear Action => SendInput (mouse/teclado).
        Console.WriteLine($"[WINDOW {handle.Id}] Action={response.Action}, reason={response.Reason}, confidence={response.Confidence}");
        return Task.CompletedTask;
    }

    public Task CloseWindowAsync(WindowHandle handle, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[WINDOW {handle.Id}] closing...");
        return Task.CompletedTask;
    }
}
