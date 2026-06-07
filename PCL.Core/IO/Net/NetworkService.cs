using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using Polly;

namespace PCL.Core.IO.Net;

[LifecycleService(LifecycleState.Loading)]
[LifecycleScope("network", "网络服务")]
public partial class NetworkService {

    private static ServiceProvider? _provider;
    private static IHttpClientFactory? _factory;
    private static readonly object _providerLock = new();

    [LifecycleStart]
    private static void _Start()
    {
        ReloadDefaultClientFactory();
    }

    public static void ReloadDefaultClientFactory()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("default")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseProxy = true,
                AutomaticDecompression = DecompressionMethods.All,
                Proxy = HttpProxyManager.Instance,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 20,
                UseCookies = false, //禁止自动 Cookie 管理
                ConnectCallback = Config.Network.EnableDoH
                    ? PCL.Core.IO.Net.Http.HostConnectionHandler.Instance.GetConnectionAsync
                    : null
            }
        );

        var provider = services.BuildServiceProvider();
        lock (_providerLock)
        {
            var oldProvider = _provider;
            _provider = provider;
            _factory = provider.GetRequiredService<IHttpClientFactory>();
            oldProvider?.Dispose();
        }
    }

    [LifecycleStop]
    private static void _Stop() {
        lock (_providerLock)
        {
            _provider?.Dispose();
            _provider = null;
            _factory = null;
        }
    }

    /// <summary>
    /// 获取 HttpClient
    /// </summary>
    /// <param name="wantClientType">指定要求的 HttpClient 来源</param>
    /// <returns>HttpClient 实例</returns>
    public static HttpClient GetClient(string wantClientType = "default")
    {
        lock (_providerLock)
        {
            return _factory?.CreateClient(wantClientType) ??
                   throw new InvalidOperationException("在初始化完成前的意外调用");
        }
    }

    private const int BaseRetryDelayMs = 1000;
    private const int MaxRetryDelayMs = 30000;

    private static TimeSpan _DefaultSleepDurationProvider(int attempt)
    {
        var delayMs = Math.Pow(2, attempt - 1) * BaseRetryDelayMs;
        delayMs = Math.Min(delayMs, MaxRetryDelayMs);
        return TimeSpan.FromMilliseconds(delayMs);
    }
    /// <summary>
    /// 获取重试策略
    /// </summary>
    /// <param name="retry">最大重试次数</param>
    /// <param name="retryPolicy">定义重试器行为</param>
    /// <returns>AsyncPolicy</returns>
    public static AsyncPolicy GetRetryPolicy(int retry = 3, Func<int,TimeSpan>? retryPolicy = null)
    {
        retryPolicy ??= _DefaultSleepDurationProvider;

        return Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retry,
                attempt => retryPolicy.Invoke(attempt),
                onRetry: (exception, timeSpan, retryAttempt, context) =>
                {
                    LogWrapper.Error(
                        exception,
                        $"HTTP 请求失败，正在进行第 {retryAttempt} 次重试，等待 {timeSpan.TotalMilliseconds} 毫秒。"
                        );
                });
    }

}
