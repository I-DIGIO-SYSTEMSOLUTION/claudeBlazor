using MudBlazor.Services;
using MyBlazorApp;
using LogicCommon;
using DatabaseLogic;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// log4net の設定 (log4net.config)。ログは <コンテンツルート>/logs/yyyyMMdd_app_<レベル>.log に出力し、設定ファイルの変更は自動で反映する
log4net.GlobalContext.Properties["LogDir"] = Path.Combine(builder.Environment.ContentRootPath, "logs");
log4net.Config.XmlConfigurator.ConfigureAndWatch(
    log4net.LogManager.GetRepository(typeof(Program).Assembly),
    new FileInfo(Path.Combine(builder.Environment.ContentRootPath, "log4net.config")));
// SQL のログにパラメータの値を出すのは開発時のみ
DatabaseConnectionHelper.EnableSensitiveDataLogging = builder.Environment.IsDevelopment();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // プロキシのタイムアウトなどで接続が切れたとき、再接続できれば入力途中の内容や処理の結果が残るよう、
        // 切断された circuit をサーバーに残しておく時間を延ばす (既定は 3 分)。ログイン状態はこれとは関係なく DB で判定する
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
    });
builder.Services.AddMudServices();
// DB 種別 (SQLServer / PostgreSQL) と接続文字列は Setting/databaseCommonConnectionSetting.xml から読み込む
builder.Services.AddDbContextFactory<DbContextImageAnnotationComment>(
    options => DatabaseAccessLogicSingleton.GetInstance.ConfigureDbContext(options));
builder.Services.AddDbContextFactory<DbContextWeatherForecast>(
    options => DatabaseAccessLogicSingleton.GetInstance.ConfigureDbContext(options));
// ログイン関係はパスワードハッシュやトークンを扱うので、開発時でも SQL のログにパラメータの値を出さない
builder.Services.AddDbContextFactory<DbContextAuth>(
    options => DatabaseAccessLogicSingleton.GetInstance.ConfigureDbContext(options, allowSensitiveDataLogging: false));
builder.Services.AddDbContext<DbContextDataProtection>(
    options => DatabaseAccessLogicSingleton.GetInstance.ConfigureDbContext(options, allowSensitiveDataLogging: false));

// ログイン Cookie などの暗号化鍵を DB (data_protection_keys) に保存する。
// 再起動やサーバーの追加でも同じ鍵を使えるので、ログイン状態が失われない (古い鍵は削除しないこと)
builder.Services.AddDataProtection()
    .SetApplicationName("MyBlazorApp")
    .PersistKeysToDbContext<DbContextDataProtection>();

// ログイン機能 (Cookie 認証・セッション管理・パスワードリセットなど)。詳細は docs/login-design.md
builder.Services.AddAppAuthentication();
// プロキシ経由の接続元 IP。appsettings.json の ForwardedHeaders に信頼するプロキシを書いたときだけ有効
var useForwardedHeaders = builder.Services.AddAppForwardedHeaders(builder.Configuration);
// Sql フォルダの SQL 文をメモリに保持し、ファイルの変更を検知して読み直す
// DB 種別 (databaseType) に応じて、DB 用の SQL ファイル (例: Search_PostgreSQL.sql) か共通のファイル (Search.sql) を使う
builder.Services.AddSingleton(sp => new SqlStatementStore(
    sp.GetRequiredService<IHostEnvironment>(),
    sp.GetRequiredService<ILogger<SqlStatementStore>>(),
    DatabaseAccessLogicSingleton.GetInstance.databaseCommonConnectionSetting?.databaseType));

var app = builder.Build();

// 最初の利用時ではなく起動時に SQL ファイルを読み込む
app.Services.GetRequiredService<SqlStatementStore>();

// 接続元 IP を X-Forwarded-For から取る (ログ・ログイン履歴・IP 単位の制限で使うので最初に置く)
if (useForwardedHeaders)
{
    app.UseForwardedHeaders();
}

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
// 認証・認可は、プロキシのプレフィックスを除去した後 (上の UseRouting の後) に行う。
// 明示しないと WebApplication がパイプラインの先頭に自動で入れてしまい、書き換え前のパスで判定される
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

// 診断ページ: Blazor の接続が無くても表示でき、プロキシ経由でどのパスが届くかを確認できる
var diagEndpoint = app.MapGet("/diag", (HttpContext ctx) =>
{
    var req = ctx.Request;
    // 全ヘッダーを表示する (Forwarded / X-Forwarded-For などで接続元 IP が届いているかの確認用)。認証情報は値を伏せる
    var headers = string.Join("\n", req.Headers
        .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
        .Select(h => h.Key is "Cookie" or "Authorization"
            ? $"{h.Key}: (省略 {h.Value.ToString().Length} 文字)"
            : $"{h.Key}: {h.Value}"));
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
        RemoteIp  : '{{ctx.Connection.RemoteIpAddress}}'
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
// 診断ページはヘッダーなど接続の情報を表示するので、Development 以外ではログインを必須にする
if (!app.Environment.IsDevelopment())
{
    diagEndpoint.RequireAuthorization();
}

// ログイン Cookie の発行 (account/signin) と削除 (account/end-session)
app.MapAuthEndpoints();

app.MapRazorComponents<MyBlazorApp.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
