#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace LogicCommon
{
    // security_policies テーブル (id = 1 の 1 行だけの設定テーブル)
    // 値を変えるときは UPDATE security_policies SET ... WHERE id = 1 で直接更新する (次の読み込みから反映される)
    [Table("security_policies")]
    public class SecurityPolicy
    {
        [Column("id")]
        public short Id { get; set; }

        // アカウントロック
        [Column("lockout_enabled")]
        public bool LockoutEnabled { get; set; }

        [Column("max_failed_attempts")]
        public int MaxFailedAttempts { get; set; }

        [Column("lockout_minutes")]
        public int LockoutMinutes { get; set; }

        [Column("failed_count_reset_minutes")]
        public int FailedCountResetMinutes { get; set; }

        // IP アドレス単位の試行制限
        [Column("ip_limit_enabled")]
        public bool IpLimitEnabled { get; set; }

        [Column("ip_max_failed_attempts")]
        public int IpMaxFailedAttempts { get; set; }

        [Column("ip_failed_window_minutes")]
        public int IpFailedWindowMinutes { get; set; }

        [Column("ip_block_minutes")]
        public int IpBlockMinutes { get; set; }

        // パスワードポリシー
        [Column("password_min_length")]
        public int PasswordMinLength { get; set; }

        [Column("password_max_length")]
        public int PasswordMaxLength { get; set; }

        [Column("require_uppercase")]
        public bool RequireUppercase { get; set; }

        [Column("require_lowercase")]
        public bool RequireLowercase { get; set; }

        [Column("require_digit")]
        public bool RequireDigit { get; set; }

        [Column("require_symbol")]
        public bool RequireSymbol { get; set; }

        [Column("min_char_types")]
        public int MinCharTypes { get; set; }

        [Column("disallow_login_id")]
        public bool DisallowLoginId { get; set; }

        [Column("password_history_count")]
        public int PasswordHistoryCount { get; set; }

        [Column("password_expiry_days")]
        public int PasswordExpiryDays { get; set; }

        // 有効期限の何日前からログイン時に変更を勧めるか (0 は知らせない)
        [Column("password_expiry_warning_days")]
        public int PasswordExpiryWarningDays { get; set; }

        // パスワードリセット
        [Column("password_reset_token_minutes")]
        public int PasswordResetTokenMinutes { get; set; }

        // セッション
        [Column("idle_timeout_minutes")]
        public int IdleTimeoutMinutes { get; set; }

        [Column("timeout_warning_seconds")]
        public int TimeoutWarningSeconds { get; set; }

        // ワンタイムパスワード (メール / SMS)
        [Column("otp_code_length")]
        public int OtpCodeLength { get; set; }

        [Column("otp_code_minutes")]
        public int OtpCodeMinutes { get; set; }

        [Column("otp_max_attempts")]
        public int OtpMaxAttempts { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
