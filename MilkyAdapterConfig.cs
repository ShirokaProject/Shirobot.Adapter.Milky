using ShiroBot.SDK.Config;

namespace ShiroBot.Adapter.Milky;

[ConfigModel]
public sealed class MilkyAdapterConfig
{
    [ConfigField("webhook：由 Milky 实现端推送事件到本地监听地址，适配器不主动连接；ws / sse：适配器主动连接 Milky 接收事件。",
        Label = "事件接收方式", Options = ["webhook", "ws", "sse"], Default = "webhook",
        Group = "connection", GroupLabel = "连接", GroupOrder = 10, Order = 10)]
    public string Protocol { get; set; } = "webhook";

    [ConfigField("Milky 实现端的 HTTP API 地址，发送消息、调用接口时使用。",
        Label = "Milky API 地址", Placeholder = "http://localhost:3010/",
        Group = "connection", GroupLabel = "连接", GroupOrder = 10, Order = 20)]
    public string BaseUrl { get; set; } = "http://localhost:3010/";

    [ConfigField("调用 Milky API 时携带的访问令牌，与实现端配置保持一致；未设置可留空。",
        Label = "API 访问令牌",
        Group = "connection", GroupLabel = "连接", GroupOrder = 10, Order = 30)]
    public string AccessToken { get; set; } = string.Empty;

    [ConfigVisibleWhen(nameof(Protocol), ConfigConditionOperator.Equal, "webhook")]
    [ConfigField("适配器监听的地址，首次启动时自动生成本机随机端口，请在 Milky 实现端把事件推送地址设为此地址。Milky 在其他机器或 Docker 中时改为 http://+:端口/（不支持 0.0.0.0）。",
        Label = "Webhook 监听地址", Placeholder = "http://127.0.0.1:端口/",
        Group = "webhook", GroupLabel = "Webhook", GroupOrder = 20, Order = 10)]
    public string WebhookUrl { get; set; } = string.Empty;

    [ConfigVisibleWhen(nameof(Protocol), ConfigConditionOperator.Equal, "webhook")]
    [ConfigField("首次启动时自动生成。Milky 推送事件时需携带 Authorization: Bearer <密钥>；清空则不校验。",
        Label = "Webhook 密钥",
        Group = "webhook", GroupLabel = "Webhook", GroupOrder = 20, Order = 20)]
    public string WebhookToken { get; set; } = string.Empty;

    [ConfigVisibleWhen(nameof(Protocol), ConfigConditionOperator.NotEqual, "webhook")]
    [ConfigField("与 Milky 的事件连接断开后是否自动重连。",
        Label = "自动重连", Default = true,
        Group = "reconnect", GroupLabel = "重连", GroupOrder = 30, Order = 10)]
    public bool ReconnectEnabled { get; set; } = true;

    [ConfigVisibleWhen(nameof(Protocol), ConfigConditionOperator.NotEqual, "webhook")]
    [ConfigEnabledWhen(nameof(ReconnectEnabled), ConfigConditionOperator.Equal, "true")]
    [ConfigField("每次重连前等待的秒数。",
        Label = "重连间隔（秒）", Min = 1, Max = 300, Default = 5,
        Group = "reconnect", GroupLabel = "重连", GroupOrder = 30, Order = 20)]
    public int ReconnectDelaySeconds { get; set; } = 5;

    [ConfigField("发送本地文件时改为 Base64 上传。Milky 实现端与 ShiroBot 不在同一台机器、无法读取本地路径时开启。",
        Label = "本地文件使用 Base64", Default = false,
        Group = "advanced", GroupLabel = "高级", GroupOrder = 40, Order = 10)]
    public bool ForceFileBase64 { get; set; } = false;

    [ConfigField("连接失败时输出完整的异常堆栈，便于排查问题。",
        Label = "详细连接错误", Default = false,
        Group = "advanced", GroupLabel = "高级", GroupOrder = 40, Order = 20)]
    public bool VerboseConnectionErrors { get; set; } = false;
}
