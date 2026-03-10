namespace BetSniffer.Ia.Configuration;

public sealed class LmStudioOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:1234";
    public string Model { get; set; } = "qwen2.5-vl-7b-instruct";
    public string ApiKey { get; set; } = "lm-studio";
}
