using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LogicCommon
{
    // SMTP でメールを送る (設定は mail_settings テーブル)
    public sealed class MailService
    {
        public async Task SendAsync(MailSetting setting, string toAddress, string subject, string body, CancellationToken cancellationToken = default)
        {
            MimeMessage message = new();
            message.From.Add(new MailboxAddress(setting.FromName ?? "", setting.FromAddress));
            message.To.Add(MailboxAddress.Parse(toAddress));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            SecureSocketOptions security = setting.SmtpSecurity switch
            {
                "NONE" => SecureSocketOptions.None,
                "SSL" => SecureSocketOptions.SslOnConnect,
                _ => SecureSocketOptions.StartTls,
            };

            using SmtpClient client = new();
            await client.ConnectAsync(setting.SmtpHost, setting.SmtpPort, security, cancellationToken);
            if (!string.IsNullOrEmpty(setting.SmtpUser))
            {
                // パスワードは DB 接続用のパスワードと同じく Auth.EncryptStringToBytes_Aes で暗号化して保存している
                string password = Auth.DecryptStringFromBytes_Aes(setting.SmtpPasswordEncrypted);
                await client.AuthenticateAsync(setting.SmtpUser, password, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
