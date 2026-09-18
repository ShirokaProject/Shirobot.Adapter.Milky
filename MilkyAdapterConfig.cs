namespace ShiroBot.Adapter.Milky;

public sealed class MilkyAdapterConfig
{
    public string Protocol { get; set; } = "ws";
    public string BaseUrl { get; set; } = "http://localhost:3010/";
    public string AccessToken { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string WebhookToken { get; set; } = string.Empty;
    public bool ForceFileBase64 { get; set; } = false;
    public bool ReconnectEnabled { get; set; } = true;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public bool VerboseConnectionErrors { get; set; } = false;
}
