#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace LogicCommon
{
    // users テーブル (ログインユーザー)
    // 読み取り専用のモデル。登録・更新は Sql/User/*.sql で行う
    // パスワード履歴 (password_history) と TOTP のリカバリーコード (mfa_recovery_codes) は、
    // 必要なときだけ別の SQL で取得するため、このクラスには含めない
    [Table("users")]
    public class User
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("login_id")]
        public string LoginId { get; set; }

        // SSO だけを使うユーザーは null
        [Column("password_hash")]
        public string PasswordHash { get; set; }

        [Column("display_name")]
        public string DisplayName { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("phone_number")]
        public string PhoneNumber { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("failed_login_count")]
        public int FailedLoginCount { get; set; }

        // timestamptz は Npgsql では Kind = Utc の DateTime になる
        [Column("last_failed_login_at")]
        public DateTime? LastFailedLoginAt { get; set; }

        [Column("locked_until")]
        public DateTime? LockedUntil { get; set; }

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }

        [Column("password_changed_at")]
        public DateTime? PasswordChangedAt { get; set; }

        // 有効期限切れの警告ダイアログを最後に表示した日時 (1 日 1 回だけ表示するため)
        [Column("password_expiry_notified_at")]
        public DateTime? PasswordExpiryNotifiedAt { get; set; }

        [Column("mfa_enabled")]
        public bool MfaEnabled { get; set; }

        [Column("mfa_method")]
        public string MfaMethod { get; set; }

        [Column("security_stamp")]
        public string SecurityStamp { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
