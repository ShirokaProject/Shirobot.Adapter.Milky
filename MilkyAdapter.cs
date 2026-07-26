using ShiroBot.MilkyAdapter.AdapterImpl;
using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.SDK.Abstractions;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Config;
using ShiroBot.SDK.Core;
using ShiroBot.SDK.Plugin;

namespace ShiroBot.MilkyAdapter;

[BotAdapter("milky",
    Name = "MilkyAdapter",
    Version = "2.0.0",
    Description = "Milky (QQ NT) adapter for ShiroBot",
    Protocol = "milky")]
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

    // ─── QQ 平台扩展服务(插件通过 context.GetAdapterExtension<IQqXxxApi>() 探测) ───
    // 通用 QQ 契约(ShiroBot.Qq.Model)实现,插件应优先使用这些接口
    private readonly QqFriendApi _qqFriendApi = new();
    private readonly QqGroupApi _qqGroupApi = new();
    private readonly QqFileApi _qqFileApi = new();
    private readonly QqSystemApi _qqSystemApi = new();
    private readonly QqMessageApi _qqMessageApi = new();

    // Milky 原生服务(仅适配器内部/需要 Milky 专有 API 时使用)
    private readonly SystemService _systemExtension = new();

    public TService? GetExtension<TService>() where TService : class =>
        this as TService
        ?? _qqFriendApi as TService
        ?? _qqGroupApi as TService
        ?? _qqFileApi as TService
        ?? _qqSystemApi as TService
        ?? _qqMessageApi as TService
        ?? _systemExtension as TService;

    private CancellationTokenSource? _eventTokenSource;

    public async Task StartAsync()
    {
        var config = Config.Load<MilkyAdapterConfig>();
        Config.Save(config);
        ResourceUriConverter.ForceFileBase64 = config.ForceFileBase64;

        MilkyClientManager.Initialize(config.BaseUrl, config.AccessToken);
        var milky = MilkyClientManager.Instance;
        _eventService.AttachEvent();

        Logger.Info("开始连接 Milky...");
        try
        {
            var loginInfo = await _systemExtension.GetLoginInfoAsync();
            MilkySession.SelfId = loginInfo.Uin.ToString();
            var result = await _systemExtension.GetImplInfoAsync();
            Logger.Success($"Milky 登录成功 - Nickname: {loginInfo.Nickname},Milky Impl: {result.ImplName} {result.ImplVersion}");
        }
        catch (Exception)
        {
            Logger.Error("Milky连接失败,请检查Adapter配置是否正确。");
            throw;
        }

        switch (config.Protocol.ToLowerInvariant())
        {
            case "sse":
                _eventTokenSource = new CancellationTokenSource();
                _ = Task.Run(() => RunEventLoopAsync(
                    "SSE",
                    token => milky.Events.ReceivingEventUsingSseAsync(token),
                    _eventTokenSource.Token));
                break;
            case "ws":
                _eventTokenSource = new CancellationTokenSource();
                _ = Task.Run(() => RunEventLoopAsync(
                    "WebSocket",
                    token => milky.Events.ReceivingEventUsingWebSocketAsync(token),
                    _eventTokenSource.Token));
                break;
            case "webhook":
                if (string.IsNullOrWhiteSpace(config.WebhookUrl))
                {
                    BotLog.Error("Webhook 模式下必须配置 WebhookUrl");
                    throw new ArgumentException("Webhook 模式下必须配置 WebhookUrl", nameof(config.WebhookUrl));
                }

                _eventTokenSource = new CancellationTokenSource();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        Logger.Info($"正在监听 Webhook 事件: {config.WebhookUrl}");
                        await milky.Events.ReceivingEventUsingWebhookAsync(config.WebhookUrl, config.WebhookToken, _eventTokenSource.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        Logger.Warning("Webhook 事件接收已取消。");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Webhook 事件接收异常: {ex.GetType().Name}: {ex.Message}");
                        Logger.Error(ex.ToString());
                        throw;
                    }
                });
                break;
            default:
                BotLog.Error("请配置正确的协议,支持的协议有Sse,WebSocket,Webhook");
                throw new ArgumentOutOfRangeException();
        }
    }

    public Task StopAsync()
    {
        _eventTokenSource?.Cancel();
        return Task.CompletedTask;
    }

    private async Task RunEventLoopAsync(
        string transportName,
        Func<CancellationToken, Task> receive,
        CancellationToken token)
    {
        var retryCount = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                retryCount++;
                Logger.Info($"正在尝试连接 {transportName} 事件流，第 {retryCount} 次。");
                await receive(token);
            }
            catch (TaskCanceledException)
            {
                Logger.Warning($"{transportName} 事件接收已取消。");
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"{transportName} 事件接收异常: {ex.GetType().Name}: {ex.Message}");
                Logger.Error(ex.ToString());
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }
}
