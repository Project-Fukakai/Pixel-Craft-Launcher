using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO.Net.Http;
using PCL.Core.Link.Natayark;
using PCL.Core.Logging;

namespace Pixel_Craft_Launcher.Services;

public static class NatayarkOAuthService
{
    public static async Task<bool> StartLoginAsync()
    {
        using var server = new CallbackServer();
        var completion = server.Completion;
        server.Start();

        var redirectUri = $"http://localhost:{server.Port}/callback";
        var url =
            "https://account.naids.com/oauth2/authorize?response_type=code" +
            $"&client_id={Secrets.NatayarkClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";

        Basics.OpenPath(url);

        var completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromMinutes(3))).ConfigureAwait(false);
        if (completed != completion.Task)
            return false;

        var code = await completion.Task.ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(code))
            return false;

        await NatayarkProfileManager.GetNaidDataAsync(code, port: server.Port).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username);
    }

    private sealed class CallbackServer : HttpServer
    {
        public TaskCompletionSource<string?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CallbackServer() : base([IPAddress.Loopback])
        {
        }

        protected override void Init()
        {
            Register(HttpMethod.Get, "/callback", HandleCallback);
            Register(HttpMethod.Get, "/complete", HandleComplete);
        }

        private Task<HttpRouteResponse> HandleCallback(HttpListenerRequest request)
        {
            try
            {
                var query = ParseQuery(request.Url?.Query);
                Completion.TrySetResult(query.TryGetValue("code", out var code) ? Uri.UnescapeDataString(code) : null);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "OAuth", "解析 Natayark OAuth 回调失败。");
                Completion.TrySetResult(null);
            }

            return HttpRouteResponse.Redirect("/complete").AsTask();
        }

        private static Task<HttpRouteResponse> HandleComplete(HttpListenerRequest request)
        {
            const string html = """
                                <!doctype html>
                                <html lang="zh-CN">
                                <head><meta charset="utf-8"><title>登录完成</title></head>
                                <body style="font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;padding:32px;">
                                <h2>Natayark 登录已完成</h2>
                                <p>你可以回到 Pixel Craft Launcher 继续操作。</p>
                                </body>
                                </html>
                                """;
            return HttpRouteResponse.Text(html, "text/html; charset=utf-8").AsTask();
        }

        private static Dictionary<string, string> ParseQuery(string? query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query))
                return result;

            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var split = part.Split('=', 2);
                if (split.Length == 2)
                    result[split[0]] = split[1];
            }

            return result;
        }
    }
}
