namespace LogicCommon
{
    // サーバーが返すリダイレクト先の URL を組み立てる。
    // ProxyBasePath が未設定 (VS Code のデバッグなど) だと Request.PathBase が空になり、"/login" のような絶対パスでは
    // プロキシのプレフィックスが抜けて 404 になる。そのため、ブラウザが今いる URL からの相対パスで返す
    // (ブラウザは Location ヘッダーの相対パスを、プレフィックス付きの今の URL を基準に解決する)
    public static class AppUrl
    {
        // 現在のリクエストのパスから、アプリ内のパス (先頭に / を付けない。例: "login?reason=timeout") への相対 URL
        // 例: /weather → "./login"、/account/signin → "../login"
        public static string RelativeFromRequest(HttpRequest request, string appPath)
        {
            string path = request.Path.HasValue ? request.Path.Value! : "/";
            int depth = path.Count(c => c == '/') - 1;
            string prefix = depth <= 0 ? "./" : string.Concat(Enumerable.Repeat("../", depth));
            return prefix + appPath;
        }

        // 現在のリクエストのアプリ内パス (先頭の / なし、クエリ文字列付き)。ログイン後の戻り先に使う
        public static string CurrentAppPath(HttpRequest request)
        {
            return (request.Path.Value ?? "/").TrimStart('/') + request.QueryString.Value;
        }

        // ログイン後の戻り先として使ってよいか (外部サイトへ飛ばされないよう、アプリ内の相対パスだけを許可する)
        // 許可しない値の場合は空文字 (= ホーム) を返す
        public static string SanitizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrEmpty(returnUrl))
            {
                return "";
            }

            // 絶対パス・プロトコル相対 (//host)・バックスラッシュ・制御文字は不可
            if (returnUrl.StartsWith('/') || returnUrl.Contains('\\') || returnUrl.Any(char.IsControl))
            {
                return "";
            }

            // "javascript:" や "https:" などのスキーム付きは不可 (パス・クエリより前に ":" があるもの)
            int colon = returnUrl.IndexOf(':');
            int pathEnd = returnUrl.IndexOfAny(['/', '?', '#']);
            if (colon >= 0 && (pathEnd < 0 || colon < pathEnd))
            {
                return "";
            }

            // ".." でアプリの外 (プロキシのプレフィックスの外) へ出るのも不可
            string pathPart = returnUrl.Split('?', '#')[0];
            if (pathPart.Split('/').Any(segment => segment == ".."))
            {
                return "";
            }

            // ログイン画面自身やサインイン処理に戻るとループするのでホームにする
            string first = pathPart.Split('/')[0];
            if (first is AuthConstants.LoginPath || pathPart.StartsWith("account/", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return returnUrl;
        }
    }
}
