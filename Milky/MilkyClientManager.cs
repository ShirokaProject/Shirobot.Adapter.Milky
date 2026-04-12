using System.Net.Http.Headers;
using ShiroBot.SDK.Abstractions;

namespace ShiroBot.MilkyAdapter.Milky;

internal static class MilkyClientManager
{
    private static MilkyClient? _instance;
    private static readonly Lock Lock = new();

    public static MilkyClient Instance =>
        _instance ?? throw new InvalidOperationException("MilkyClient has not been initialized. Call Initialize() first.");

    public static void Initialize(string baseAddress, string? authToken = null)
    {
        lock (Lock)
        {
            if (_instance != null)
            {
                BotLog.Info("MilkyClientManager: 客户端已初始化，跳过重复初始化");
                return;
            }
            
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(string.IsNullOrWhiteSpace(baseAddress) ? 
                    throw new ArgumentException("Milky BaseUrl 不能为空。", nameof(baseAddress)) :
                    baseAddress.Trim().TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(600)
            };
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Milky.Net.Client/1.2");
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }
            _instance = new MilkyClient(httpClient);
        }
    }
}
