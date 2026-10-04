using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LogicCommon
{
    // ログイン Cookie の発行と削除を行う HTTP のエンドポイント。
    // Interactive Server の画面 (SignalR の通信) からは Cookie を発行・削除できないので、画面からはここへ forceLoad で遷移する
    // リダイレクト先はプロキシ配下でも正しく解決されるよう、今の URL からの相対パスで返す (AppUrl を参照)
    public static class AuthEndpoints
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AuthEndpoints));

        public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            // GET account/signin?ticket=...&returnUrl=...
            // ログイン画面で本人確認が済んだら、発行されたチケットを付けてここへ遷移する。セッションを作って Cookie を発行する
            app.MapGet("/" + AuthConstants.SignInPath, async (HttpContext context, string? ticket, string? returnUrl,
                LoginService loginService, SessionService sessionService) =>
            {
                SignInTicketInfo? info = await loginService.RedeemSignInTicketAsync(ticket);
                if (info == null)
                {
                    // 期限切れ (1 分) や使用済みのチケット。ログインし直してもらう
                    return Results.Redirect(AppUrl.RelativeFromRequest(context.Request, $"{AuthConstants.LoginPath}?reason=expired"));
                }

                string? ipAddress = context.Connection.RemoteIpAddress?.ToString();
                string userAgent = context.Request.Headers.UserAgent.ToString();
                Guid sessionId = await sessionService.CreateAsync(info.User.Id, info.AuthMethod, info.MfaMethod, ipAddress, userAgent);

                List<Claim> claims =
                [
                    new(ClaimTypes.NameIdentifier, info.User.Id.ToString()),
                    new(ClaimTypes.Name, info.User.DisplayName),
                    new(AuthConstants.LoginIdClaim, info.User.LoginId),
                    new(AuthConstants.SessionIdClaim, sessionId.ToString()),
                ];
                claims.AddRange((await loginService.GetRoleCodesAsync(info.User.Id)).Select(code => new Claim(ClaimTypes.Role, code)));

                // ブラウザを閉じると消える Cookie にする。ログアウトの判定は DB の user_sessions で行う
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                    new AuthenticationProperties { IsPersistent = false });

                logger.Info($"ログインしました (ユーザーID {info.User.Id}, 方式 {info.AuthMethod})。");
                return Results.Redirect(AppUrl.RelativeFromRequest(context.Request, AppUrl.SanitizeReturnUrl(returnUrl)));
            });

            // GET account/end-session?reason=timeout|ended
            // ログアウトボタン・自動ログアウトのときにここへ遷移する。セッションを終了して Cookie を削除し、ログイン画面へ戻す
            // (GET なので第三者のサイトからログアウトさせることはできるが、それ以上の影響はない)
            app.MapGet("/" + AuthConstants.LogoutPath, async (HttpContext context, string? reason, SessionService sessionService) =>
            {
                if (Guid.TryParse(context.User.FindFirst(AuthConstants.SessionIdClaim)?.Value, out Guid sessionId))
                {
                    // 自動ログアウトの場合は IDLE_TIMEOUT で終了済み。それ以外は LOGOUT で終了する (終了済みなら何もしない)
                    await sessionService.EndAsync(sessionId, SessionEndReasons.Logout);
                    logger.Info($"ログアウトしました (ユーザーID {context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value}, 理由 {reason ?? "ボタン"})。");
                }
                else
                {
                    // Cookie が届かなかった (既に期限切れ・削除済み) 場合。原因を調べるときのために残す
                    logger.Info($"ログアウトの要求がありましたが、ログイン Cookie がありませんでした (理由 {reason ?? "ボタン"})。");
                }
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                string query = reason is "timeout" or "ended" ? $"?reason={reason}" : "";
                return Results.Redirect(AppUrl.RelativeFromRequest(context.Request, AuthConstants.LoginPath + query));
            });
        }
    }
}
