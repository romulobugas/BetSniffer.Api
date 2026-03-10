namespace BetSniffer.Ia.Configuration;

public sealed class AgentOptions
{
    public int ParallelWindows { get; set; } = 5;
    public int StepDelayMs { get; set; } = 350;
    public int MaxStepsPerJob { get; set; } = 120;
    public Viewport DefaultViewport { get; set; } = new();
}

public sealed class Viewport
{
    public int Width { get; set; } = 1366;
    public int Height { get; set; } = 768;
}
