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
    Version = "2.0.1",
    Description = "Milky (QQ NT) adapter for ShiroBot",
    Author = "ShirokaProject",
    GithubRepo = "https://github.com/ShirokaProject/Shirobot.Adapter.Milky",
    Protocol = "milky",
    ProtocolVersionRange = ">=1.2.0 <1.4.0",
    IsSingleFile = true,
    SharedAssemblies = "ShiroBot.Model.QQ")]
public class MilkyAdapter : IBotAdapter
{
    internal const string SupportedMilkyVersionRange = "1.2.x - 1.3.x";

    private static readonly Version MinimumMilkyVersion = new(1, 2, 0);
    private static readonly Version FirstUntestedMilkyVersion = new(1, 4, 0);
    private readonly EventService _eventService = new();
    private readonly QFriendApi _qFriendApi = new();
    private readonly QGroupApi _qGroupApi = new();
    private readonly QFileApi _qFileApi = new();
    private readonly QSystemApi _qSystemApi = new();
    private readonly QMessageApi _qMessageApi = new();
    private readonly SystemService _systemExtension = new();
    private CancellationTokenSource? _eventTokenSource;
    private Task? _eventLoopTask;

    public string Platform => MilkyMapper.PlatformId;
    public IMessageService Message { get; } = new CoreMessageService();
    public IChannelService Channel { get; } = new CoreChannelService();
    public IUserService User { get; } = new CoreUserService();
    public IEventService Event => _eventService;
    public IConfigContext Config { get; set; } = null!;
    public IConsoleLogger Logger { get; set; } = null!;

    public TService? GetExtension<TService>() where TService : class =>
        this as TService
        ?? _qFriendApi as TService
        ?? _qGroupApi as TService
        ?? _qFileApi as TService
        ?? _qSystemApi as TService
        ?? _qMessageApi as TService
        ?? _systemExtension as TService;

    public async Task StartAsync()
    {
        await StopAsync().ConfigureAwait(false);
        var config = Config.Load<MilkyAdapterConfig>();
        Config.Save(config);
        ResourceUriConverter.ForceFileBase64 = config.ForceFileBase64;

        MilkyClientManager.Initialize(config.BaseUrl, config.AccessToken);
        _eventService.AttachEvent();
        _eventTokenSource = new CancellationTokenSource();
        _eventLoopTask = RunConnectionLifecycleAsync(config, _eventTokenSource.Token);
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
                var loginInfo = await _systemExtension.GetLoginInfoAsync().ConfigureAwait(false);
                MilkySession.SelfId = loginInfo.Uin.ToString();
                var result = await _systemExtension.GetImplInfoAsync().ConfigureAwait(false);
                ValidateMilkyVersion(result.MilkyVersion);
                Logger.Success(
                    $"Milky 登录成功 - Nickname: {loginInfo.Nickname}, Milky Impl: {result.ImplName} {result.ImplVersion}, MilkyVersion: {result.MilkyVersion}");
                attempt = 0;
                await ReceiveEventsAsync(config, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested) Logger.Warning("Milky 事件连接已断开，准备重连。");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Milky 连接失败: {ex.GetType().Name}: {ex.Message}");
                if (config.VerboseConnectionErrors) Logger.Error(ex.ToString());
            }

            if (!config.ReconnectEnabled || token.IsCancellationRequested) break;

            var delaySeconds = Math.Clamp(config.ReconnectDelaySeconds, 1, 300);
            Logger.Warning($"将在 {delaySeconds} 秒后重试 Milky 连接。");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
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
        if (_eventTokenSource is not null) await _eventTokenSource.CancelAsync().ConfigureAwait(false);

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

    private void ValidateMilkyVersion(string milkyVersion)
    {
        Logger.Info($"服务端 MilkyVersion: {milkyVersion}; 适配器支持范围: {SupportedMilkyVersionRange}。");
        if (!TryParseMilkyVersion(milkyVersion, out var version, out var isPreRelease))
        {
            Logger.Warning($"无法解析服务端 MilkyVersion '{milkyVersion}'，将继续启动，但兼容性未经确认。");
            return;
        }

        if (isPreRelease)
            Logger.Warning($"服务端 MilkyVersion {milkyVersion} 是预发布版本，将按低于同核心稳定版本的保守策略评估。");

        if (IsBelowMinimumMilkyVersion(version, isPreRelease))
        {
            var message = $"服务端 MilkyVersion {milkyVersion} 低于最低支持版本 {MinimumMilkyVersion}，拒绝启动。";
            Logger.Error(message);
            throw new NotSupportedException(message);
        }

        if (version >= FirstUntestedMilkyVersion)
            Logger.Warning($"服务端 MilkyVersion {milkyVersion} 高于已验证范围 {SupportedMilkyVersionRange}，将按向前兼容策略继续启动。");
    }

    internal static bool TryParseMilkyVersion(string value, out Version version, out bool isPreRelease)
    {
        version = new Version();
        isPreRelease = false;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Trim().TrimStart('v', 'V');
        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            if (suffixIndex == 0 || suffixIndex == normalized.Length - 1) return false;
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
            return false;

        version = new Version(major, minor, patch);
        return true;
    }

    internal static bool IsBelowMinimumMilkyVersion(Version version, bool isPreRelease) =>
        version < MinimumMilkyVersion || isPreRelease && version == MinimumMilkyVersion;
}
