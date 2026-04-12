namespace ShiroBot.MilkyAdapter;

public sealed class MilkyAdapterConfig
{
    public string Protocol { get; set; } = "ws";
    public string? WebhookUrl { get; set; }
    public string? WebhookToken { get; set; }
    public string BaseUrl { get; set; } = "http://localhost:3010/";
    public string AccessToken { get; set; } = "";
}
