#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace LogicCommon
{
    // user_sessions テーブル (ログイン 1 回につき 1 行。全タブで共有する)
    [Table("user_sessions")]
    public class UserSession
    {
        // Cookie に入れるセッション ID
        [Column("id")]
        public Guid Id { get; set; }

        [Column("user_id")]
        public long UserId { get; set; }

        [Column("auth_method")]
        public string AuthMethod { get; set; }

        [Column("mfa_method")]
        public string MfaMethod { get; set; }

        [Column("logged_in_at")]
        public DateTime LoggedInAt { get; set; }

        // どのタブの操作でも更新する
        [Column("last_activity_at")]
        public DateTime LastActivityAt { get; set; }

        // サーバー処理の生存通知・終了日時。アイドル時間は LastActivityAt とこの値の新しい方から数える
        [Column("processing_heartbeat_at")]
        public DateTime? ProcessingHeartbeatAt { get; set; }

        [Column("ended_at")]
        public DateTime? EndedAt { get; set; }

        [Column("end_reason")]
        public string EndReason { get; set; }

        [Column("ip_address")]
        public string IpAddress { get; set; }

        [Column("user_agent")]
        public string UserAgent { get; set; }
    }
}
