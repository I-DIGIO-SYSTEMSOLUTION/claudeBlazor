using MudBlazor.Services;
using MyBlazorApp;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

var app = builder.Build();

// 診断用: 届いたリクエストのパスと結果をデバッグコンソールに出す
app.Use(async (context, next) =>
{
    var raw = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
    await next(context);
    app.Logger.LogInformation("[REQ] {Raw} -> {Status}", raw, context.Response.StatusCode);
});

// SageMaker Code Editor のポートプロキシ (例: /codeeditor/default/proxy/5000) 配下で動かすための設定。
// プロキシがプレフィックスを「除去する (proxy)」「除去しない (absproxy)」どちらでも動くよう、
//  - プレフィックス付きで届いたリクエストは除去して PathBase へ移す
//  - プレフィックス無しで届いたリクエストも PathBase を補う
// ことで、サーバーが生成する URL をブラウザから見たパスに合わせる。
var proxyBasePath = app.Configuration["ProxyBasePath"]?.TrimEnd('/');
if (!string.IsNullOrEmpty(proxyBasePath))
{
    app.Use((context, next) =>
    {
        if (context.Request.Path.StartsWithSegments(proxyBasePath, out var remaining))
        {
            context.Request.Path = remaining;
        }
        context.Request.PathBase = proxyBasePath;
        return next(context);
    });
    app.Logger.LogInformation("ProxyBasePath = {ProxyBasePath}", proxyBasePath);
    app.Logger.LogInformation("ブラウザで https://<StudioのドメインURL>{ProxyBasePath}/ を開いてください", proxyBasePath);

    // パス書き換え後にルーティングさせる (明示しないとルーティングがこのミドルウェアより先に走る)
    app.UseRouting();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

// 診断ページ: Blazor の接続が無くても表示でき、プロキシ経由でどのパスが届くかを確認できる
app.MapGet("/diag", (HttpContext ctx) =>
{
    var req = ctx.Request;
    var headers = string.Join("\n", req.Headers
        .Where(h => h.Key is "Host" || h.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase))
        .Select(h => $"{h.Key}: {h.Value}"));
    var built = File.GetLastWriteTime(typeof(Program).Assembly.Location);
    var html = $$"""
        <!DOCTYPE html>
        <html><head><meta charset="utf-8"><title>diag</title></head>
        <body style="font-family:monospace;white-space:pre-wrap">
        <b>[server]</b>
        build     : {{built:yyyy/MM/dd HH:mm:ss}}
        PathBase  : '{{req.PathBase}}'
        Path      : '{{req.Path}}'
        RawTarget : '{{ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestFeature>()?.RawTarget}}'
        ProxyBasePath(config) : '{{app.Configuration["ProxyBasePath"]}}'
        {{System.Net.WebUtility.HtmlEncode(headers)}}

        <b>[browser]</b>
        <span id="out">running...</span>
        <script>
        (async () => {
          const lines = ["location : " + location.href];
          const prefix = (location.pathname.match(/^(.*?\/(?:abs)?proxy\/\d+)(\/|$)/) || [])[1] || "(proxy パターンなし)";
          lines.push("prefix   : " + prefix);
          const tests = ["_framework/blazor.web.js", "app.css"];
          for (const t of tests) {
            const u = prefix.startsWith("/") ? prefix + "/" + t : t;
            try { const r = await fetch(u, { redirect: "manual" }); lines.push("GET  " + u + " -> " + r.status + " " + r.type); }
            catch (e) { lines.push("GET  " + u + " -> ERROR " + e); }
          }
          const n = prefix.startsWith("/") ? prefix + "/_blazor/negotiate?negotiateVersion=1" : "_blazor/negotiate?negotiateVersion=1";
          try { const r = await fetch(n, { method: "POST", redirect: "manual" }); lines.push("POST " + n + " -> " + r.status + " " + r.type); }
          catch (e) { lines.push("POST " + n + " -> ERROR " + e); }
          document.getElementById("out").textContent = lines.join("\n");
        })();
        </script>
        </body></html>
        """;
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapRazorComponents<MyBlazorApp.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
