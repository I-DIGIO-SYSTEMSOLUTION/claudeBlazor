#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace LogicCommon
{
    // user_tokens テーブル (1 回限り・期限付きのトークンとワンタイムパスワード)
    // トークンの平文は保存せず、SHA-256 のハッシュ (16 進) だけを持つ
    [Table("user_tokens")]
    public class UserToken
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("user_id")]
        public long UserId { get; set; }

        // UserTokenPurposes の値
        [Column("purpose")]
        public string Purpose { get; set; }

        [Column("token_hash")]
        public string TokenHash { get; set; }

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("used_at")]
        public DateTime? UsedAt { get; set; }

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        [Column("attempt_count")]
        public int AttemptCount { get; set; }

        [Column("ip_address")]
        public string IpAddress { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
