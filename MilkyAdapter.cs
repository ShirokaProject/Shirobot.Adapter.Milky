using ShiroBot.MilkyAdapter.AdapterImpl;
using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.SDK.Abstractions;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Config;
using ShiroBot.SDK.Core;
using ShiroBot.SDK.Plugin;

namespace ShiroBot.MilkyAdapter;

[BotAdapter(
    "MilkyAdapter",
    Name = "MilkyAdapter",
    Version = "1.3.0-rc2",
    Description = "Milky Adapter for ShiroBot",
    Author = "ShiroBot",
    GithubRepo = "https://github.com/ShirokaProject/Shirobot.Adapter.Milky",
    Protocol = "Milky",
    ProtocolVersionRange = ">=1.2.0 <1.4.0",
    IsSingleFile = true)]
public class MilkyAdapter : IBotAdapter
{
    internal const string SupportedMilkyVersionRange = "1.2.x - 1.3.x";

    private static readonly Version MinimumMilkyVersion = new(1, 2, 0);
    private static readonly Version FirstUntestedMilkyVersion = new(1, 4, 0);

    public string Name => "MilkyAdapter";
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
    private Task? _eventTask;

    public async Task StartAsync()
    {
        await StopAsync().ConfigureAwait(false);
        var config = Config.Load<MilkyAdapterConfig>();
        Config.Save(config);
        ResourceUriConverter.ForceFileBase64 = config.ForceFileBase64;

        MilkyClientManager.Initialize(config.BaseUrl, config.AccessToken);
        var milky = MilkyClientManager.Instance;
        _eventService.AttachEvent();
        
        Logger.Info("开始连接 Milky...");
        try
        {
            var loginInfo = await System.GetLoginInfoAsync();
            var result = await System.GetImplInfoAsync();
            ValidateMilkyVersion(result.MilkyVersion);
            Logger.Success(
                $"Milky 登录成功 - Nickname: {loginInfo.Nickname}, Milky Impl: {result.ImplName} {result.ImplVersion}, MilkyVersion: {result.MilkyVersion}");
        }
        catch (Exception)
        {
            Logger.Error("Milky连接失败,请检查Adapter配置是否正确。");
            throw;
        }

        switch (config.Protocol.ToLowerInvariant())
        {
            case "sse":
                var sseCancellation = new CancellationTokenSource();
                _eventTokenSource = sseCancellation;
                _eventTask = Task.Run(async () =>
                {
                    var retryCount = 0;
                    while (!sseCancellation.Token.IsCancellationRequested)
                    {
                        try
                        {
                            retryCount++;
                            Logger.Info($"正在尝试连接 SSE 事件流，第 {retryCount} 次。");
                            await milky.Events.ReceivingEventUsingSseAsync(sseCancellation.Token);
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
                                await Task.Delay(TimeSpan.FromSeconds(5), sseCancellation.Token);
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
                var wsCancellation = new CancellationTokenSource();
                _eventTokenSource = wsCancellation;
                _eventTask = Task.Run(async () =>
                {
                    var retryCount = 0;
                    while (!wsCancellation.Token.IsCancellationRequested)
                    {
                        try
                        {
                            retryCount++;
                            Logger.Info($"正在尝试连接 WebSocket 事件流，第 {retryCount} 次。");
                            await milky.Events.ReceivingEventUsingWebSocketAsync(wsCancellation.Token);
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
                                await Task.Delay(TimeSpan.FromSeconds(5), wsCancellation.Token);
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

                var webhookCancellation = new CancellationTokenSource();
                _eventTokenSource = webhookCancellation;
                var webhookStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _eventTask = Task.Run(async () =>
                {
                    try
                    {
                        Logger.Info($"正在监听 Webhook 事件: {config.WebhookUrl}");
                        await milky.Events.ReceivingEventUsingWebhookAsync(
                            config.WebhookUrl,
                            config.WebhookToken,
                            webhookCancellation.Token,
                            webhookStarted);
                    }
                    catch (TaskCanceledException)
                    {
                        Logger.Warning("Webhook 事件接收已取消。");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Webhook 事件接收异常: {ex.GetType().Name}: {ex.Message}");
                        Logger.Error(ex.ToString());
                    }
                });
                await webhookStarted.Task.ConfigureAwait(false);
                break;
            default:
                BotLog.Error("请配置正确的协议,支持的协议有SSE、WebSocket、Webhook");
                throw new ArgumentOutOfRangeException();
        }
    }

    public async Task StopAsync()
    {
        var cancellation = _eventTokenSource;
        var task = _eventTask;
        _eventTokenSource = null;
        _eventTask = null;
        if (cancellation is null) return;

        cancellation.Cancel();
        try
        {
            if (task is not null) await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void ValidateMilkyVersion(string milkyVersion)
    {
        Logger.Info($"服务端 MilkyVersion: {milkyVersion}; 适配器支持范围: {SupportedMilkyVersionRange}。");

        if (!TryParseMilkyVersion(milkyVersion, out var version, out var isPreRelease))
        {
            Logger.Warning($"无法解析服务端 MilkyVersion '{milkyVersion}'，将继续启动，但兼容性未经确认。");
            return;
        }

        if (isPreRelease)
        {
            Logger.Warning($"服务端 MilkyVersion {milkyVersion} 是预发布版本，将按低于同核心稳定版本的保守策略评估。");
        }

        if (IsBelowMinimumMilkyVersion(version, isPreRelease))
        {
            var message = $"服务端 MilkyVersion {milkyVersion} 低于最低支持版本 {MinimumMilkyVersion}，拒绝启动。";
            Logger.Error(message);
            throw new NotSupportedException(message);
        }

        if (version >= FirstUntestedMilkyVersion)
        {
            Logger.Warning(
                $"服务端 MilkyVersion {milkyVersion} 高于已验证范围 {SupportedMilkyVersionRange}，将按向前兼容策略继续启动。");
        }
    }

    internal static bool TryParseMilkyVersion(string value, out Version version, out bool isPreRelease)
    {
        version = new Version();
        isPreRelease = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            if (suffixIndex == 0 || suffixIndex == normalized.Length - 1)
            {
                return false;
            }

            isPreRelease = normalized[suffixIndex] == '-';
            normalized = normalized[..suffixIndex];
        }

        var parts = normalized.Split('.');
        var patch = 0;
        if (parts.Length is < 2 or > 3 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            parts.Length > 2 && !int.TryParse(parts[2], out patch) ||
            major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new Version(major, minor, patch);
        return true;
    }

    internal static bool IsBelowMinimumMilkyVersion(Version version, bool isPreRelease) =>
        version < MinimumMilkyVersion || isPreRelease && version == MinimumMilkyVersion;
}
