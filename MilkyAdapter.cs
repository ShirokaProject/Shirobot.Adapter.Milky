using ShiroBot.Adapter.Milky.AdapterImpl;
using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.SDK.Abstractions;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Config;
using ShiroBot.SDK.Core;
using ShiroBot.SDK.Plugin;

[assembly: ShiroBotApiCompatibility("0.9", "0.9")]

namespace ShiroBot.Adapter.Milky;

[BotAdapter("milky",
    Name = "MilkyAdapter",
    Version = "2.0.0",
    Description = "Milky (QQ NT) adapter for ShiroBot",
    Protocol = "milky",
    SharedAssemblies = "ShiroBot.Model.QQ")]
public class MilkyAdapter : IBotAdapter
{
    private readonly EventService _eventService = new();

    public string Platform => MilkyMapper.PlatformId;

    public IMessageService Message { get; } = new CoreMessageService();
    public IChannelService Channel { get; } = new CoreChannelService();
    public IUserService User { get; } = new CoreUserService();
    public IEventService Event => _eventService;
    public IConfigContext Config { get; set; } = null!;
    public IConsoleLogger Logger { get; set; } = null!;

    // ─── QQ 平台扩展服务(插件通过 context.GetAdapterExtension<IQXxxApi>() 探测) ───
    // QQ contract (ShiroBot.Model.QQ) implementation; plugins should prefer these interfaces.
    private readonly QFriendApi _qFriendApi = new();
    private readonly QGroupApi _qGroupApi = new();
    private readonly QFileApi _qFileApi = new();
    private readonly QSystemApi _qSystemApi = new();
    private readonly QMessageApi _qMessageApi = new();

    // Milky 原生服务(仅适配器内部/需要 Milky 专有 API 时使用)
    private readonly SystemService _systemExtension = new();

    public TService? GetExtension<TService>() where TService : class =>
        this as TService
        ?? _qFriendApi as TService
        ?? _qGroupApi as TService
        ?? _qFileApi as TService
        ?? _qSystemApi as TService
        ?? _qMessageApi as TService
        ?? _systemExtension as TService;

    private CancellationTokenSource? _eventTokenSource;
    private Task? _eventLoopTask;

    public async Task StartAsync()
    {
        var config = Config.Load<MilkyAdapterConfig>();
        Config.Save(config);
        ResourceUriConverter.ForceFileBase64 = config.ForceFileBase64;

        MilkyClientManager.Initialize(config.BaseUrl, config.AccessToken);
        _eventService.AttachEvent();
        _eventTokenSource = new CancellationTokenSource();
        _eventLoopTask = RunConnectionLifecycleAsync(config, _eventTokenSource.Token);
        await Task.CompletedTask;
    }

    private async Task RunConnectionLifecycleAsync(MilkyAdapterConfig config, CancellationToken token)
    {
        var attempt = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                attempt++;
                Logger.Info($"开始连接 Milky，第 {attempt} 次尝试...");
                var loginInfo = await _systemExtension.GetLoginInfoAsync();
                MilkySession.SelfId = loginInfo.Uin.ToString();
                var result = await _systemExtension.GetImplInfoAsync();
                Logger.Success($"Milky 登录成功 - Nickname: {loginInfo.Nickname}, Milky Impl: {result.ImplName} {result.ImplVersion}");
                attempt = 0;
                await ReceiveEventsAsync(config, token);
                if (!token.IsCancellationRequested)
                {
                    Logger.Warning("Milky 事件连接已断开，准备重连。");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Milky 连接失败: {ex.GetType().Name}: {ex.Message}");
                if (config.VerboseConnectionErrors)
                {
                    Logger.Error(ex.ToString());
                }
            }

            if (!config.ReconnectEnabled || token.IsCancellationRequested)
            {
                break;
            }

            var delaySeconds = Math.Clamp(config.ReconnectDelaySeconds, 1, 300);
            Logger.Warning($"将在 {delaySeconds} 秒后重试 Milky 连接。");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
        }
    }

    private Task ReceiveEventsAsync(MilkyAdapterConfig config, CancellationToken token) =>
        config.Protocol.ToLowerInvariant() switch
        {
            "sse" => MilkyClientManager.Instance.Events.ReceivingEventUsingSseAsync(token),
            "ws" or "websocket" => MilkyClientManager.Instance.Events.ReceivingEventUsingWebSocketAsync(token),
            "webhook" when !string.IsNullOrWhiteSpace(config.WebhookUrl) =>
                MilkyClientManager.Instance.Events.ReceivingEventUsingWebhookAsync(
                    config.WebhookUrl,
                    config.WebhookToken,
                    token),
            "webhook" => throw new ArgumentException("Webhook 模式下必须配置 WebhookUrl", nameof(config.WebhookUrl)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(config.Protocol),
                config.Protocol,
                "支持 sse、ws、websocket、webhook。")
        };

    public async Task StopAsync()
    {
        if (_eventTokenSource is not null)
        {
            await _eventTokenSource.CancelAsync();
        }

        if (_eventLoopTask is not null)
        {
            try
            {
                await _eventLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected while stopping the active transport.
            }
        }

        _eventService.DetachEvent();
        _eventTokenSource?.Dispose();
        _eventTokenSource = null;
        _eventLoopTask = null;
        MilkyClientManager.Reset();
    }

}
