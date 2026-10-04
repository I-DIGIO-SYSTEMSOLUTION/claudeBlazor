using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;

namespace LogicCommon
{
    // users.password_history の 1 要素 (新しい順に並べ、先頭が現在のパスワード)
    public sealed record PasswordHistoryEntry(string Hash, DateTime ChangedAt, long? ChangedBy, string Reason);

    // パスワードのハッシュ化・ポリシーのチェック・履歴の読み書き
    public sealed class PasswordPolicy
    {
        // JSON は {"hash","changedAt","changedBy","reason"} の形 (camelCase)
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        // ハッシュ化は ASP.NET Core Identity の PasswordHasher だけを使う (Identity のテーブルは使わない)
        private readonly PasswordHasher<User> hasher = new();

        // ログイン ID が存在しないときも同じだけ時間をかけるためのダミーのハッシュ
        // (応答時間の差からログイン ID の有無を推測されないようにする)
        private readonly Lazy<string> dummyHash;

        public PasswordPolicy()
        {
            dummyHash = new(() => hasher.HashPassword(null!, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))));
        }

        public string Hash(string password) => hasher.HashPassword(null!, password);

        public bool Verify(string? passwordHash, string password)
        {
            if (string.IsNullOrEmpty(passwordHash))
            {
                // パスワード未設定 (SSO のみのユーザー) でも、照合と同じだけ時間をかける
                hasher.VerifyHashedPassword(null!, dummyHash.Value, password);
                return false;
            }
            return hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
        }

        // 存在しないログイン ID のときに呼ぶ (結果は常に不一致)
        public void VerifyDummy(string password) => hasher.VerifyHashedPassword(null!, dummyHash.Value, password);

        // パスワードがポリシーを満たしているか。満たしていない理由のメッセージを返す (空なら OK)
        // 過去のパスワードとの重複は CheckHistory で別にチェックする
        public List<string> Validate(SecurityPolicy policy, string? password, string? loginId)
        {
            List<string> errors = new();
            password ??= "";

            if (password.Length < policy.PasswordMinLength)
            {
                errors.Add($"パスワードは {policy.PasswordMinLength} 文字以上にしてください。");
            }
            if (password.Length > policy.PasswordMaxLength)
            {
                errors.Add($"パスワードは {policy.PasswordMaxLength} 文字以下にしてください。");
            }

            CharTypes types = GetCharTypes(password);
            if (policy.RequireUppercase && !types.Upper) errors.Add("英大文字を含めてください。");
            if (policy.RequireLowercase && !types.Lower) errors.Add("英小文字を含めてください。");
            if (policy.RequireDigit && !types.Digit) errors.Add("数字を含めてください。");
            if (policy.RequireSymbol && !types.Symbol) errors.Add("記号を含めてください。");
            if (types.Count < policy.MinCharTypes)
            {
                errors.Add($"英大文字・英小文字・数字・記号のうち {policy.MinCharTypes} 種類以上を含めてください。");
            }

            if (policy.DisallowLoginId && !string.IsNullOrEmpty(loginId) &&
                password.Contains(loginId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("ログイン ID を含むパスワードは使えません。");
            }

            return errors;
        }

        // 現在を含む直近 password_history_count 世代のパスワードと同じなら、エラーメッセージを返す
        public string? CheckHistory(SecurityPolicy policy, IReadOnlyList<PasswordHistoryEntry> history, string? currentHash, string password)
        {
            if (policy.PasswordHistoryCount <= 0)
            {
                return null;
            }

            IEnumerable<string> hashes = history.Take(policy.PasswordHistoryCount).Select(e => e.Hash);
            if (!string.IsNullOrEmpty(currentHash))
            {
                // 履歴の先頭は現在のパスワードのはずだが、念のため現在のハッシュとも照合する
                hashes = hashes.Prepend(currentHash).Distinct();
            }

            foreach (string hash in hashes)
            {
                if (hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed)
                {
                    return $"直近 {policy.PasswordHistoryCount} 回と同じパスワードは使えません。";
                }
            }
            return null;
        }

        // 画面に出すポリシーの説明
        public static string Describe(SecurityPolicy policy)
        {
            List<string> rules = [$"{policy.PasswordMinLength} 文字以上 {policy.PasswordMaxLength} 文字以下"];

            List<string> required = new();
            if (policy.RequireUppercase) required.Add("英大文字");
            if (policy.RequireLowercase) required.Add("英小文字");
            if (policy.RequireDigit) required.Add("数字");
            if (policy.RequireSymbol) required.Add("記号");
            if (required.Count > 0) rules.Add($"{string.Join("・", required)}を必ず含む");

            if (policy.MinCharTypes > 0) rules.Add($"英大文字・英小文字・数字・記号のうち {policy.MinCharTypes} 種類以上");
            if (policy.DisallowLoginId) rules.Add("ログイン ID を含まない");
            if (policy.PasswordHistoryCount > 0) rules.Add($"直近 {policy.PasswordHistoryCount} 回と同じパスワードは不可");

            return string.Join("、", rules);
        }

        // ---- 履歴 (JSON) ----

        public static List<PasswordHistoryEntry> ParseHistory(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new();
            }
            return JsonSerializer.Deserialize<List<PasswordHistoryEntry>>(json, JsonOptions) ?? new();
        }

        // 新しいパスワードを先頭に追加した履歴の JSON (全件残す)
        public static string PrependHistory(IReadOnlyList<PasswordHistoryEntry> history, PasswordHistoryEntry entry)
        {
            return JsonSerializer.Serialize(history.Prepend(entry).ToList(), JsonOptions);
        }

        // ---- 文字種 ----

        private readonly record struct CharTypes(bool Upper, bool Lower, bool Digit, bool Symbol)
        {
            public int Count => (Upper ? 1 : 0) + (Lower ? 1 : 0) + (Digit ? 1 : 0) + (Symbol ? 1 : 0);
        }

        // 英字・数字は半角だけを数える。記号は半角の英数字・空白以外の表示できる ASCII 文字 (! " # $ % など)
        private static CharTypes GetCharTypes(string password)
        {
            bool upper = false, lower = false, digit = false, symbol = false;
            foreach (char c in password)
            {
                if (c is >= 'A' and <= 'Z') upper = true;
                else if (c is >= 'a' and <= 'z') lower = true;
                else if (c is >= '0' and <= '9') digit = true;
                else if (c is > ' ' and < (char)0x7F) symbol = true;
            }
            return new(upper, lower, digit, symbol);
        }
    }

    // トークン (リセットリンク・サインインのチケットなど) の生成とハッシュ化
    public static class TokenGenerator
    {
        // URL に載せられる、推測できないランダムな文字列 (256 ビット)
        public static string Create() =>
            System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        // DB に保存する SHA-256 ハッシュ (16 進の小文字 64 文字)
        public static string Hash(string token) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
