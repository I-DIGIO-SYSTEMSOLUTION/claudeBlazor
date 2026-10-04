#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace LogicCommon
{
    // mail_settings テーブル (id = 1 の 1 行だけの設定テーブル)
    [Table("mail_settings")]
    public class MailSetting
    {
        [Column("id")]
        public short Id { get; set; }

        [Column("smtp_host")]
        public string SmtpHost { get; set; }

        [Column("smtp_port")]
        public int SmtpPort { get; set; }

        // NONE / STARTTLS / SSL
        [Column("smtp_security")]
        public string SmtpSecurity { get; set; }

        // 認証なしは null
        [Column("smtp_user")]
        public string SmtpUser { get; set; }

        // Auth.EncryptStringToBytes_Aes で暗号化した値
        [Column("smtp_password_encrypted")]
        public string SmtpPasswordEncrypted { get; set; }

        [Column("from_address")]
        public string FromAddress { get; set; }

        [Column("from_name")]
        public string FromName { get; set; }

        // メール本文のリンクに使う URL (プロキシのプレフィックスまで含め、末尾は /)
        [Column("public_base_url")]
        public string PublicBaseUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
