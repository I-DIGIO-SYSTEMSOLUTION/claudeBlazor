namespace LogicCommon
{
    // ログイン機能で使う定数。DB の CHECK 制約の値と合わせる (docs/login-design.md を参照)
    public static class AuthConstants
    {
        // ログイン Cookie の名前 (テーマ用の MyBlazorApp.Theme と同じく path=/ で発行する)
        public const string CookieName = "MyBlazorApp.Auth";

        // Cookie に入れるクレーム
        public const string SessionIdClaim = "session_id";
        public const string LoginIdClaim = "login_id";

        // 管理者ロール (roles.code)。ユーザー管理画面はこのロールだけが使える
        public const string AdminRole = "ADMIN";

        // ログイン画面などのアプリ内パス (プロキシ配下で動かすため先頭に / を付けない)
        public const string LoginPath = "login";
        // URL に "logout" を含めない。SageMaker のプロキシ配下では、.../proxy/5000/account/logout へのアクセスが
        // アプリに届かず、SageMaker 自身の認証が切れる (Invalid or expired auth token) 現象があったため
        public const string LogoutPath = "account/end-session";
        public const string SignInPath = "account/signin";
        public const string ResetPasswordPath = "reset-password";
        public const string AccessDeniedPath = "access-denied";
    }

    // 本人確認の方式 (user_sessions.auth_method / login_histories.auth_method)
    public static class AuthMethods
    {
        public const string Password = "PASSWORD";
        public const string Oidc = "OIDC";
    }

    // ログイン失敗の理由 (login_histories.failure_reason)
    public static class LoginFailureReasons
    {
        public const string InvalidPassword = "INVALID_PASSWORD";
        public const string Locked = "LOCKED";
        public const string Inactive = "INACTIVE";
        public const string NotFound = "NOT_FOUND";
        public const string IpBlocked = "IP_BLOCKED";
        public const string MfaFailed = "MFA_FAILED";
        public const string SsoFailed = "SSO_FAILED";
    }

    // セッションの終了理由 (user_sessions.end_reason)
    public static class SessionEndReasons
    {
        public const string Logout = "LOGOUT";
        public const string IdleTimeout = "IDLE_TIMEOUT";
        public const string Revoked = "REVOKED";
        public const string PasswordChanged = "PASSWORD_CHANGED";
    }

    // トークンの用途 (user_tokens.purpose)
    public static class UserTokenPurposes
    {
        public const string PasswordReset = "PASSWORD_RESET";
        public const string SignInTicket = "SIGNIN_TICKET";
        public const string MfaPending = "MFA_PENDING";
        public const string MfaEmail = "MFA_EMAIL";
        public const string MfaSms = "MFA_SMS";
    }

    // パスワードを設定した理由 (users.password_history の reason)
    public static class PasswordChangeReasons
    {
        public const string Initial = "INITIAL";
        public const string UserChange = "USER_CHANGE";
        public const string AdminReset = "ADMIN_RESET";
        public const string PasswordReset = "PASSWORD_RESET";
        public const string Expired = "EXPIRED";
    }
}
