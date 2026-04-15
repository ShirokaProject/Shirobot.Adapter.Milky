using ShiroBot.MilkyAdapter.AdapterImpl;
using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.SDK.Abstractions;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Config;
using ShiroBot.SDK.Core;
using ShiroBot.SDK.Plugin;

namespace ShiroBot.MilkyAdapter;

public class MilkyAdapter : IBotAdapter
{
    public string Name => "MilkyAdapter";
    public BotComponentMetadata Metadata { get; } = new()
    {
        Name = "MilkyAdapter",
        Version = "1.2.2",
        Description = "Milky Adapter for ShiroBot"
    };

    private readonly EventService _eventService = new();

    public IFileService File { get; } = new FileService();
    public IFriendService Friend { get; } = new FriendService();
    public IGroupService Group { get; } = new GroupService();
    public IMessageService Message { get; } = new MessageService();
    public ISystemService System { get; } = new SystemService();
    public IEventService Event => _eventService;
    public IConfigContext Config { get; set; } = null!;
    public IConsoleLogger Logger { get; set; } = null!;

    private CancellationTokenSource? _eventTokenSource;

    public async Task StartAsync()
    {
        var config = Config.Load<MilkyAdapterConfig>();
        Config.Save(config);

        MilkyClientManager.Initialize(config.BaseUrl, config.AccessToken);
        var milky = MilkyClientManager.Instance;
        _eventService.AttachEvent();
        
        Logger.Info("开始连接 Milky...");
        try
        {
            var loginInfo = await System.GetLoginInfoAsync();
            var result = await System.GetImplInfoAsync();
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
                _ = Task.Run(async () =>
                {
                    var retryCount = 0;
                    while (!_eventTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            retryCount++;
                            Logger.Info($"正在尝试连接 SSE 事件流，第 {retryCount} 次。");
                            await milky.Events.ReceivingEventUsingSseAsync(_eventTokenSource.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            Logger.Warning("SSE 事件接收已取消。");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"SSE 事件接收异常: {ex.GetType().Name}: {ex.Message}");
                            Logger.Error(ex.ToString());
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(5), _eventTokenSource.Token);
                            }
                            catch (TaskCanceledException)
                            {
                                break;
                            }
                        }
                    }
                });
                break;
            case "ws":
                _eventTokenSource = new CancellationTokenSource();
                _ = Task.Run(async () =>
                {
                    var retryCount = 0;
                    while (!_eventTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            retryCount++;
                            Logger.Info($"正在尝试连接 WebSocket 事件流，第 {retryCount} 次。");
                            await milky.Events.ReceivingEventUsingWebSocketAsync(_eventTokenSource.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            Logger.Warning("WebSocket 事件接收已取消。");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"WebSocket 事件接收异常: {ex.GetType().Name}: {ex.Message}");
                            Logger.Error(ex.ToString());
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(5), _eventTokenSource.Token);
                            }
                            catch (TaskCanceledException)
                            {
                                break;
                            }
                        }
                    }
                });
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
                BotLog.Error("请配置正确的协议,支持的协议有Sse,WebSocket");
                throw new ArgumentOutOfRangeException();
        }
    }
}

