using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Memory;

namespace LogicCommon
{
    // ログイン機能のサービス登録 (Program.cs から呼ぶ)
    public static class AuthServiceCollectionExtensions
    {
        // HTTP リクエストごとのセッションの確認結果 (有効だった場合だけ) を持っておく時間。
        // 静的ファイルを含むすべてのリクエストで Cookie を確認するので、毎回 DB を読まないようにする
        private static readonly TimeSpan ValidSessionCacheDuration = TimeSpan.FromSeconds(15);

        public static IServiceCollection AddAppAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = AuthConstants.CookieName;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    // HTTPS で届いたリクエスト (プロキシの X-Forwarded-Proto を含む) のときだけ Secure を付ける
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    // プロキシがプレフィックスを除去する場合もしない場合も送られるよう、ドメイン直下にする
                    options.Cookie.Path = "/";

                    // 最大ログイン時間は設けない。Cookie はブラウザを閉じると消え (IsPersistent = false)、
                    // 操作が無いことによるログアウトは DB の user_sessions で判定する。そのためチケットの期限は十分長くしておく
                    options.ExpireTimeSpan = TimeSpan.FromDays(365);
                    options.SlidingExpiration = false;

                    options.Events = new CookieAuthenticationEvents
                    {
                        OnValidatePrincipal = ValidateSessionAsync,
                        OnRedirectToLogin = context =>
                        {
                            string returnUrl = Uri.EscapeDataString(AppUrl.CurrentAppPath(context.Request));
                            context.Response.Redirect(AppUrl.RelativeFromRequest(context.Request,
                                $"{AuthConstants.LoginPath}?returnUrl={returnUrl}"));
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = context =>
                        {
                            context.Response.Redirect(AppUrl.RelativeFromRequest(context.Request, AuthConstants.AccessDeniedPath));
                            return Task.CompletedTask;
                        },
                    };
                });

            services.AddAuthorization();
            services.AddCascadingAuthenticationState();
            services.AddMemoryCache();
            services.AddHttpContextAccessor();

            services.AddSingleton<AuthRepository>();
            services.AddSingleton<PasswordPolicy>();
            services.AddSingleton<SessionProcessingTracker>();
            services.AddSingleton<SessionService>();
            services.AddSingleton<MailService>();
            services.AddSingleton<UserAccountService>();
            services.AddSingleton<PasswordResetService>();
            services.AddSingleton<LoginService>();
            services.AddScoped<ClientConnectionInfo>();
            services.AddScoped<CircuitHandler, ClientConnectionCircuitHandler>();
            services.AddHostedService<SessionMaintenanceService>();

            return services;
        }

        // HTTP リクエストのたびに、Cookie のセッションがまだ有効かを DB で確認する
        // (ログアウト済み・操作が無く期限切れ・ユーザーの無効化・パスワード変更で終了したセッションは、Cookie を削除する)
        private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
        {
            if (!Guid.TryParse(context.Principal?.FindFirst(AuthConstants.SessionIdClaim)?.Value, out Guid sessionId))
            {
                await RejectAsync(context);
                return;
            }

            IMemoryCache cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            string cacheKey = $"session-valid:{sessionId}";
            if (cache.TryGetValue(cacheKey, out _))
            {
                return;
            }

            SessionService sessionService = context.HttpContext.RequestServices.GetRequiredService<SessionService>();
            SessionStatus status = await sessionService.GetStatusAsync(sessionId);
            if (status.IsValid)
            {
                cache.Set(cacheKey, true, ValidSessionCacheDuration);
                return;
            }
            await RejectAsync(context);
        }

        private static async Task RejectAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        // プロキシ経由の接続元 IP (X-Forwarded-For) を使う設定。appsettings.json の ForwardedHeaders に
        // 信頼するプロキシ (KnownProxies) かネットワーク (KnownNetworks) を書いたときだけ有効にする。
        // 信頼するプロキシ以外が付けた X-Forwarded-For は使わない (偽造して IP 単位の制限をすり抜けられないように)。
        // 有効にした場合は true を返す (Program.cs でパイプラインの最初に UseForwardedHeaders を入れる)
        public static bool AddAppForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
        {
            IConfigurationSection section = configuration.GetSection("ForwardedHeaders");
            string[] proxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
            string[] networks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];
            if (proxies.Length == 0 && networks.Length == 0)
            {
                return false;
            }

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // 既定で信頼されている localhost も、設定に書いたものだけにする
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                foreach (string proxy in proxies)
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }
                foreach (string network in networks)
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
                // プロキシの段数。X-Forwarded-For の右端から、この数まで信頼するプロキシをたどる
                options.ForwardLimit = section.GetValue<int?>("ForwardLimit") ?? 1;
            });
            return true;
        }
    }
}
