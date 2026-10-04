-- ログイン機能のテーブル (SQL Server)。設計は docs/login-design.md。PostgreSQL 版は CreateAuthTables_PostgreSQL.sql
-- 外部キーは設定しない (テーブル間の整合性はアプリ側で守る)。テーブルはどの順番で作ってもよい
-- 最初の管理者は、アプリの setup 画面から登録する (パスワードのハッシュをアプリで作る必要があるため)
--
-- PostgreSQL 版との違い
--   - 日時は datetime2 に UTC で保存する (アプリの Entity が DateTime のため。datetimeoffset は DateTime として読めない)
--   - JSON の列 (jsonb) は nvarchar(max) にし、CHECK 制約で JSON の配列であることを確かめる
--   - users のメールアドレスの一意制約は、小文字にした計算列 email_normalized に付ける (式のインデックスが作れないため)
--   - 部分インデックスはフィルター付きインデックスにする
--   - コメント (COMMENT ON) は拡張プロパティ MS_Description にする。スキーマは dbo を前提にしている

-- ============================================================
-- セキュリティポリシー (id = 1 の 1 行だけの設定テーブル)
-- ============================================================
CREATE TABLE security_policies (
    id                           smallint      NOT NULL DEFAULT 1,

    -- アカウントロック
    lockout_enabled              bit           NOT NULL DEFAULT 1,
    max_failed_attempts          int           NOT NULL DEFAULT 5,
    lockout_minutes              int           NOT NULL DEFAULT 10,
    failed_count_reset_minutes   int           NOT NULL DEFAULT 30,

    -- IP アドレス単位の試行制限
    ip_limit_enabled             bit           NOT NULL DEFAULT 1,
    ip_max_failed_attempts       int           NOT NULL DEFAULT 20,
    ip_failed_window_minutes     int           NOT NULL DEFAULT 10,
    ip_block_minutes             int           NOT NULL DEFAULT 10,

    -- パスワードポリシー
    password_min_length          int           NOT NULL DEFAULT 8,
    password_max_length          int           NOT NULL DEFAULT 128,
    require_uppercase            bit           NOT NULL DEFAULT 0,
    require_lowercase            bit           NOT NULL DEFAULT 0,
    require_digit                bit           NOT NULL DEFAULT 0,
    require_symbol               bit           NOT NULL DEFAULT 0,
    min_char_types               int           NOT NULL DEFAULT 3,
    disallow_login_id            bit           NOT NULL DEFAULT 1,
    password_history_count       int           NOT NULL DEFAULT 3,
    password_expiry_days         int           NOT NULL DEFAULT 0,

    -- パスワードリセット
    password_reset_token_minutes int           NOT NULL DEFAULT 30,

    -- セッション
    idle_timeout_minutes         int           NOT NULL DEFAULT 30,
    timeout_warning_seconds      int           NOT NULL DEFAULT 60,

    -- ワンタイムパスワード (メール / SMS)
    otp_code_length              int           NOT NULL DEFAULT 6,
    otp_code_minutes             int           NOT NULL DEFAULT 5,
    otp_max_attempts             int           NOT NULL DEFAULT 5,

    created_at                   datetime2     NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at                   datetime2     NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT pk_security_policies PRIMARY KEY (id),
    CONSTRAINT ck_security_policies_single_row      CHECK (id = 1),
    CONSTRAINT ck_security_policies_max_failed      CHECK (max_failed_attempts >= 1),
    CONSTRAINT ck_security_policies_lockout_min     CHECK (lockout_minutes >= 1),
    CONSTRAINT ck_security_policies_reset_min       CHECK (failed_count_reset_minutes >= 0),
    CONSTRAINT ck_security_policies_ip_max_failed   CHECK (ip_max_failed_attempts >= 1),
    CONSTRAINT ck_security_policies_ip_window       CHECK (ip_failed_window_minutes >= 1),
    CONSTRAINT ck_security_policies_ip_block        CHECK (ip_block_minutes >= 1),
    CONSTRAINT ck_security_policies_length          CHECK (password_min_length >= 1
                                                       AND password_max_length >= password_min_length),
    CONSTRAINT ck_security_policies_char_types      CHECK (min_char_types BETWEEN 0 AND 4),
    CONSTRAINT ck_security_policies_history         CHECK (password_history_count >= 0),
    CONSTRAINT ck_security_policies_expiry          CHECK (password_expiry_days >= 0),
    CONSTRAINT ck_security_policies_reset_token     CHECK (password_reset_token_minutes >= 1),
    CONSTRAINT ck_security_policies_idle_timeout    CHECK (idle_timeout_minutes >= 1),
    CONSTRAINT ck_security_policies_timeout_warning CHECK (timeout_warning_seconds >= 0
                                                       AND timeout_warning_seconds < idle_timeout_minutes * 60),
    CONSTRAINT ck_security_policies_otp_length      CHECK (otp_code_length BETWEEN 4 AND 10),
    CONSTRAINT ck_security_policies_otp_minutes     CHECK (otp_code_minutes >= 1),
    CONSTRAINT ck_security_policies_otp_attempts    CHECK (otp_max_attempts >= 1)
);

INSERT INTO security_policies (id) VALUES (1);

-- ============================================================
-- メール設定 (id = 1 の 1 行だけの設定テーブル)
-- ============================================================
CREATE TABLE mail_settings (
    id                      smallint       NOT NULL DEFAULT 1,
    smtp_host               nvarchar(255)  NOT NULL,
    smtp_port               int            NOT NULL DEFAULT 587,
    smtp_security           nvarchar(10)   NOT NULL DEFAULT N'STARTTLS',
    smtp_user               nvarchar(255)  NULL,
    smtp_password_encrypted nvarchar(512)  NULL,
    from_address            nvarchar(256)  NOT NULL,
    from_name               nvarchar(100)  NULL,
    public_base_url         nvarchar(512)  NOT NULL,
    created_at              datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at              datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_mail_settings PRIMARY KEY (id),
    CONSTRAINT ck_mail_settings_single_row CHECK (id = 1),
    CONSTRAINT ck_mail_settings_port       CHECK (smtp_port BETWEEN 1 AND 65535),
    CONSTRAINT ck_mail_settings_security   CHECK (smtp_security IN (N'NONE', N'STARTTLS', N'SSL'))
);

-- 値は環境に合わせて設定する
-- INSERT INTO mail_settings (smtp_host, smtp_port, smtp_security, from_address, from_name, public_base_url)
-- VALUES (N'smtp.example.com', 587, N'STARTTLS', N'no-reply@example.com', N'MyBlazorApp', N'https://.../codeeditor/default/proxy/5000/');

-- ============================================================
-- ユーザー
-- ============================================================
CREATE TABLE users (
    id                    bigint         IDENTITY(1, 1) NOT NULL,
    login_id              nvarchar(256)  NOT NULL,
    password_hash         nvarchar(255)  NULL,
    password_history      nvarchar(max)  NOT NULL DEFAULT N'[]',
    display_name          nvarchar(100)  NOT NULL,
    email                 nvarchar(256)  NOT NULL,
    -- メールアドレスの一意制約用 (大文字小文字を区別しない)
    email_normalized      AS LOWER(email) PERSISTED,
    phone_number          nvarchar(20)   NULL,
    is_active             bit            NOT NULL DEFAULT 1,
    failed_login_count    int            NOT NULL DEFAULT 0,
    last_failed_login_at  datetime2      NULL,
    locked_until          datetime2      NULL,
    last_login_at         datetime2      NULL,
    password_changed_at   datetime2      NULL,
    mfa_enabled           bit            NOT NULL DEFAULT 0,
    mfa_method            nvarchar(10)   NULL,
    totp_secret_encrypted nvarchar(512)  NULL,
    mfa_recovery_codes    nvarchar(max)  NOT NULL DEFAULT N'[]',
    security_stamp        nvarchar(64)   NOT NULL DEFAULT CONVERT(nvarchar(64), NEWID()),
    created_at            datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at            datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT uq_users_login_id                 UNIQUE (login_id),
    CONSTRAINT ck_users_failed_login_count       CHECK (failed_login_count >= 0),
    CONSTRAINT ck_users_password_history_array   CHECK (ISJSON(password_history) = 1 AND LEFT(LTRIM(password_history), 1) = N'['),
    CONSTRAINT ck_users_mfa_recovery_codes_array CHECK (ISJSON(mfa_recovery_codes) = 1 AND LEFT(LTRIM(mfa_recovery_codes), 1) = N'['),
    CONSTRAINT ck_users_mfa_method               CHECK (mfa_method IN (N'TOTP', N'EMAIL', N'SMS')),
    CONSTRAINT ck_users_mfa_enabled              CHECK (mfa_enabled = 0 OR mfa_method IS NOT NULL),
    CONSTRAINT ck_users_mfa_totp                 CHECK (mfa_method <> N'TOTP' OR totp_secret_encrypted IS NOT NULL),
    CONSTRAINT ck_users_mfa_sms                  CHECK (mfa_method <> N'SMS'  OR phone_number IS NOT NULL)
);

CREATE UNIQUE INDEX uq_users_email ON users (email_normalized);

-- ============================================================
-- 外部ログイン (OIDC のアカウントと users の紐付け)
-- ============================================================
CREATE TABLE user_external_logins (
    id               bigint         IDENTITY(1, 1) NOT NULL,
    user_id          bigint         NOT NULL,
    provider         nvarchar(50)   NOT NULL,
    provider_subject nvarchar(255)  NOT NULL,
    email_at_link    nvarchar(256)  NULL,
    linked_at        datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    last_login_at    datetime2      NULL,
    CONSTRAINT pk_user_external_logins PRIMARY KEY (id),
    CONSTRAINT uq_user_external_logins_provider_subject UNIQUE (provider, provider_subject)
);

CREATE INDEX ix_user_external_logins_user_id ON user_external_logins (user_id);

-- ============================================================
-- ユーザーセッション
-- ============================================================
CREATE TABLE user_sessions (
    id                      uniqueidentifier NOT NULL DEFAULT NEWID(),
    user_id                 bigint           NOT NULL,
    auth_method             nvarchar(20)     NOT NULL DEFAULT N'PASSWORD',
    mfa_method              nvarchar(20)     NULL,
    logged_in_at            datetime2        NOT NULL DEFAULT SYSUTCDATETIME(),
    last_activity_at        datetime2        NOT NULL DEFAULT SYSUTCDATETIME(),
    processing_heartbeat_at datetime2        NULL,
    ended_at                datetime2        NULL,
    end_reason              nvarchar(20)     NULL,
    ip_address              nvarchar(45)     NULL,
    user_agent              nvarchar(512)    NULL,
    -- uniqueidentifier の主キーはクラスター化すると挿入のたびにページ分割が起きるので、非クラスター化にする
    CONSTRAINT pk_user_sessions PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT ck_user_sessions_auth_method CHECK (auth_method IN (N'PASSWORD', N'OIDC')),
    CONSTRAINT ck_user_sessions_mfa_method  CHECK (mfa_method IN (N'TOTP', N'EMAIL', N'SMS', N'RECOVERY_CODE')),
    CONSTRAINT ck_user_sessions_end_reason  CHECK (end_reason IN (N'LOGOUT', N'IDLE_TIMEOUT', N'REVOKED', N'PASSWORD_CHANGED')),
    CONSTRAINT ck_user_sessions_ended
        CHECK ((ended_at IS NULL AND end_reason IS NULL) OR (ended_at IS NOT NULL AND end_reason IS NOT NULL))
);

CREATE INDEX ix_user_sessions_user_id ON user_sessions (user_id);
CREATE INDEX ix_user_sessions_active  ON user_sessions (user_id) WHERE ended_at IS NULL;

-- ============================================================
-- ユーザートークン (1 回限り・期限付きのトークンとコード)
-- ============================================================
CREATE TABLE user_tokens (
    id            bigint         IDENTITY(1, 1) NOT NULL,
    user_id       bigint         NOT NULL,
    purpose       nvarchar(20)   NOT NULL,
    token_hash    nvarchar(64)   NOT NULL,
    expires_at    datetime2      NOT NULL,
    used_at       datetime2      NULL,
    revoked_at    datetime2      NULL,
    attempt_count int            NOT NULL DEFAULT 0,
    ip_address    nvarchar(45)   NULL,
    created_at    datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_user_tokens PRIMARY KEY (id),
    CONSTRAINT ck_user_tokens_purpose
        CHECK (purpose IN (N'PASSWORD_RESET', N'SIGNIN_TICKET', N'MFA_PENDING', N'MFA_EMAIL', N'MFA_SMS')),
    CONSTRAINT ck_user_tokens_attempt_count CHECK (attempt_count >= 0)
);

CREATE INDEX ix_user_tokens_token_hash      ON user_tokens (token_hash);
CREATE INDEX ix_user_tokens_user_id_purpose ON user_tokens (user_id, purpose) WHERE used_at IS NULL AND revoked_at IS NULL;
CREATE INDEX ix_user_tokens_expires_at      ON user_tokens (expires_at);

-- ============================================================
-- ログイン履歴
-- ============================================================
CREATE TABLE login_histories (
    id             bigint         IDENTITY(1, 1) NOT NULL,
    user_id        bigint         NULL,
    login_id       nvarchar(256)  NOT NULL,
    auth_method    nvarchar(20)   NOT NULL DEFAULT N'PASSWORD',
    succeeded      bit            NOT NULL,
    failure_reason nvarchar(20)   NULL,
    ip_address     nvarchar(45)   NULL,
    user_agent     nvarchar(512)  NULL,
    attempted_at   datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_login_histories PRIMARY KEY (id),
    CONSTRAINT ck_login_histories_auth_method
        CHECK (auth_method IN (N'PASSWORD', N'OIDC')),
    CONSTRAINT ck_login_histories_failure_reason
        CHECK (failure_reason IN (N'INVALID_PASSWORD', N'LOCKED', N'INACTIVE', N'NOT_FOUND', N'IP_BLOCKED', N'MFA_FAILED', N'SSO_FAILED')),
    CONSTRAINT ck_login_histories_result
        CHECK ((succeeded = 1 AND failure_reason IS NULL) OR (succeeded = 0 AND failure_reason IS NOT NULL))
);

CREATE INDEX ix_login_histories_attempted_at      ON login_histories (attempted_at);
CREATE INDEX ix_login_histories_user_id_attempted ON login_histories (user_id, attempted_at);
CREATE INDEX ix_login_histories_ip_failed         ON login_histories (ip_address, attempted_at) WHERE succeeded = 0;

-- ============================================================
-- ロール
-- ============================================================
CREATE TABLE roles (
    id         bigint         IDENTITY(1, 1) NOT NULL,
    code       nvarchar(50)   NOT NULL,
    name       nvarchar(100)  NOT NULL,
    created_at datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_roles PRIMARY KEY (id),
    CONSTRAINT uq_roles_code UNIQUE (code)
);

-- ============================================================
-- ユーザーロール
-- ============================================================
CREATE TABLE user_roles (
    user_id    bigint     NOT NULL,
    role_id    bigint     NOT NULL,
    created_at datetime2  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_user_roles PRIMARY KEY (user_id, role_id)
);

CREATE INDEX ix_user_roles_role_id ON user_roles (role_id);

-- ============================================================
-- Data Protection の鍵 (Cookie・TOTP シークレットの暗号化鍵)
-- ============================================================
CREATE TABLE data_protection_keys (
    id            int            IDENTITY(1, 1) NOT NULL,
    friendly_name nvarchar(max)  NULL,
    xml           nvarchar(max)  NULL,
    CONSTRAINT pk_data_protection_keys PRIMARY KEY (id)
);

-- ============================================================
-- コメント (拡張プロパティ MS_Description)
-- ============================================================

EXEC sys.sp_addextendedproperty N'MS_Description', N'セキュリティポリシー (id = 1 の 1 行のみ)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ID (常に 1)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'アカウントロックを行うか', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'lockout_enabled';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ロックするまでの連続失敗回数', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'max_failed_attempts';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ロック時間 (分)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'lockout_minutes';
EXEC sys.sp_addextendedproperty N'MS_Description', N'最後の失敗からこの分数が経過したら失敗回数を 0 に戻す (0 はリセットしない)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'failed_count_reset_minutes';
EXEC sys.sp_addextendedproperty N'MS_Description', N'IP アドレス単位の試行制限を行うか', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'ip_limit_enabled';
EXEC sys.sp_addextendedproperty N'MS_Description', N'同一 IP からの失敗がこの回数に達したらブロック', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'ip_max_failed_attempts';
EXEC sys.sp_addextendedproperty N'MS_Description', N'失敗回数を数える期間 (分)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'ip_failed_window_minutes';
EXEC sys.sp_addextendedproperty N'MS_Description', N'IP ブロック時間 (分)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'ip_block_minutes';
EXEC sys.sp_addextendedproperty N'MS_Description', N'パスワード最小文字数', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'password_min_length';
EXEC sys.sp_addextendedproperty N'MS_Description', N'パスワード最大文字数', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'password_max_length';
EXEC sys.sp_addextendedproperty N'MS_Description', N'英大文字を必須とするか', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'require_uppercase';
EXEC sys.sp_addextendedproperty N'MS_Description', N'英小文字を必須とするか', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'require_lowercase';
EXEC sys.sp_addextendedproperty N'MS_Description', N'数字を必須とするか', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'require_digit';
EXEC sys.sp_addextendedproperty N'MS_Description', N'記号を必須とするか', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'require_symbol';
EXEC sys.sp_addextendedproperty N'MS_Description', N'英大文字・英小文字・数字・記号のうち最低何種類を含めるか (0 は制限なし)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'min_char_types';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ログインID を含むパスワードを禁止するか', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'disallow_login_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'現在のパスワードを含む直近何世代の再利用を禁止するか (0 は制限なし)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'password_history_count';
EXEC sys.sp_addextendedproperty N'MS_Description', N'パスワード有効期限 (日、0 は無期限)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'password_expiry_days';
EXEC sys.sp_addextendedproperty N'MS_Description', N'パスワードリセットリンクの有効期限 (分)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'password_reset_token_minutes';
EXEC sys.sp_addextendedproperty N'MS_Description', N'最後の操作 (またはサーバー処理の終了) からログアウトまでの時間 (分)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'idle_timeout_minutes';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ログアウト前に警告を出す秒数 (0 は警告なし)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'timeout_warning_seconds';
EXEC sys.sp_addextendedproperty N'MS_Description', N'メール / SMS で送るワンタイムパスワードの桁数', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'otp_code_length';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ワンタイムパスワードの有効期限 (分)', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'otp_code_minutes';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ワンタイムパスワード 1 件あたりの入力可能回数', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'otp_max_attempts';
EXEC sys.sp_addextendedproperty N'MS_Description', N'作成日時', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'created_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'更新日時', N'SCHEMA', N'dbo', N'TABLE', N'security_policies', N'COLUMN', N'updated_at';

EXEC sys.sp_addextendedproperty N'MS_Description', N'メール設定 (id = 1 の 1 行のみ)', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ID (常に 1)', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'SMTP サーバー', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'smtp_host';
EXEC sys.sp_addextendedproperty N'MS_Description', N'SMTP ポート', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'smtp_port';
EXEC sys.sp_addextendedproperty N'MS_Description', N'暗号化方式 (NONE / STARTTLS / SSL)', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'smtp_security';
EXEC sys.sp_addextendedproperty N'MS_Description', N'SMTP 認証ユーザー (認証なしは NULL)', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'smtp_user';
EXEC sys.sp_addextendedproperty N'MS_Description', N'SMTP 認証パスワード (Auth.EncryptStringToBytes_Aes で暗号化)', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'smtp_password_encrypted';
EXEC sys.sp_addextendedproperty N'MS_Description', N'送信元メールアドレス', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'from_address';
EXEC sys.sp_addextendedproperty N'MS_Description', N'送信元の表示名', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'from_name';
EXEC sys.sp_addextendedproperty N'MS_Description', N'メール本文のリンクに使う URL (例: https://<Studio のドメイン>/codeeditor/default/proxy/5000/)', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'public_base_url';
EXEC sys.sp_addextendedproperty N'MS_Description', N'作成日時', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'created_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'更新日時', N'SCHEMA', N'dbo', N'TABLE', N'mail_settings', N'COLUMN', N'updated_at';

EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザー', N'SCHEMA', N'dbo', N'TABLE', N'users';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーID', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ログインID (SSO で自動登録したユーザーは登録時のメールアドレス。以後変更しない)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'login_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'パスワードハッシュ (PasswordHasher の出力。ソルトを含む。SSO のみの利用者は NULL)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'password_hash';
EXEC sys.sp_addextendedproperty N'MS_Description', N'パスワード設定履歴 (全件・新しい順。先頭が現在のパスワード。要素: {"hash","changedAt","changedBy","reason"})', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'password_history';
EXEC sys.sp_addextendedproperty N'MS_Description', N'表示名', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'display_name';
EXEC sys.sp_addextendedproperty N'MS_Description', N'メールアドレス (大文字小文字を区別せず一意。パスワードリセット・SSO の紐付けに使用)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'email';
EXEC sys.sp_addextendedproperty N'MS_Description', N'メールアドレスを小文字にした値 (一意制約用の計算列。SQL Server 版のみ)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'email_normalized';
EXEC sys.sp_addextendedproperty N'MS_Description', N'電話番号 (SMS によるワンタイムパスワード用、E.164 形式)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'phone_number';
EXEC sys.sp_addextendedproperty N'MS_Description', N'有効フラグ (false で無効化。物理削除はしない)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'is_active';
EXEC sys.sp_addextendedproperty N'MS_Description', N'連続ログイン失敗回数', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'failed_login_count';
EXEC sys.sp_addextendedproperty N'MS_Description', N'最後にログインに失敗した日時', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'last_failed_login_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ロックアウト解除日時 (この日時まではログイン不可)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'locked_until';
EXEC sys.sp_addextendedproperty N'MS_Description', N'最終ログイン日時', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'last_login_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'パスワード変更日時 (有効期限の判定用)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'password_changed_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'追加認証 (ワンタイムパスワード) を行うか', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'mfa_enabled';
EXEC sys.sp_addextendedproperty N'MS_Description', N'追加認証の方式 (TOTP / EMAIL / SMS)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'mfa_method';
EXEC sys.sp_addextendedproperty N'MS_Description', N'TOTP のシークレット (ASP.NET Core Data Protection で暗号化。用途: MyBlazorApp.TotpSecret)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'totp_secret_encrypted';
EXEC sys.sp_addextendedproperty N'MS_Description', N'TOTP のリカバリーコード (ハッシュ化した値の配列。使用済みは削除)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'mfa_recovery_codes';
EXEC sys.sp_addextendedproperty N'MS_Description', N'セキュリティスタンプ (パスワード変更・リセット時に更新し、既存のセッションを無効化する)', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'security_stamp';
EXEC sys.sp_addextendedproperty N'MS_Description', N'作成日時', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'created_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'更新日時', N'SCHEMA', N'dbo', N'TABLE', N'users', N'COLUMN', N'updated_at';

EXEC sys.sp_addextendedproperty N'MS_Description', N'外部ログイン (OIDC のアカウントと users の紐付け。1 ユーザーに複数可)', N'SCHEMA', N'dbo', N'TABLE', N'user_external_logins';
EXEC sys.sp_addextendedproperty N'MS_Description', N'外部ログインID', N'SCHEMA', N'dbo', N'TABLE', N'user_external_logins', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーID (users.id)', N'SCHEMA', N'dbo', N'TABLE', N'user_external_logins', N'COLUMN', N'user_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'プロバイダー名 (設定ファイルの名前。例: EntraID)', N'SCHEMA', N'dbo', N'TABLE', N'user_external_logins', N'COLUMN', N'provider';
EXEC sys.sp_addextendedproperty N'MS_Description', N'プロバイダー側のユーザー識別子 (OIDC の sub クレーム)', N'SCHEMA', N'dbo', N'TABLE', N'user_external_logins', N'COLUMN', N'provider_subject';
EXEC sys.sp_addextendedproperty N'MS_Description', N'紐付けたときのメールアドレス (監査用)', N'SCHEMA', N'dbo', N'TABLE', N'user_external_logins', N'COLUMN', N'email_at_link';
EXEC sys.sp_addextendedproperty N'MS_Description', N'紐付け日時', N'SCHEMA', N'dbo', N'TABLE', N'user_external_logins', N'COLUMN', N'linked_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'このプロバイダーでの最終ログイン日時', N'SCHEMA', N'dbo', N'TABLE', N'user_external_logins', N'COLUMN', N'last_login_at';

EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーセッション (ログイン 1 回につき 1 行。全タブで共有)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions';
EXEC sys.sp_addextendedproperty N'MS_Description', N'セッションID (Cookie に格納)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーID (users.id)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'user_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'本人確認の方式 (PASSWORD / OIDC)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'auth_method';
EXEC sys.sp_addextendedproperty N'MS_Description', N'追加認証の方式 (TOTP / EMAIL / SMS / RECOVERY_CODE。追加認証なしは NULL)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'mfa_method';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ログイン日時', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'logged_in_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'最終操作日時 (クリック・キー入力・画面遷移・マウス移動・スクロール。どのタブの操作でも更新)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'last_activity_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'サーバー処理の生存通知・終了日時 (処理中は定期的に更新し、終了時に終了日時を記録。アイドル時間は last_activity_at とこの値の新しい方から数える)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'processing_heartbeat_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'セッション終了日時 (NULL はログイン中)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'ended_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'終了理由 (LOGOUT / IDLE_TIMEOUT / REVOKED / PASSWORD_CHANGED)', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'end_reason';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ログイン時の接続元IPアドレス', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'ip_address';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ログイン時の User-Agent', N'SCHEMA', N'dbo', N'TABLE', N'user_sessions', N'COLUMN', N'user_agent';

EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザートークン (1 回限り・期限付きのトークンとワンタイムパスワード)', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens';
EXEC sys.sp_addextendedproperty N'MS_Description', N'トークンID', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーID (users.id)', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'user_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'用途 (PASSWORD_RESET: リセットリンク / SIGNIN_TICKET: 画面からサインイン処理への受け渡し / MFA_PENDING: 本人確認済み・追加認証待ち / MFA_EMAIL, MFA_SMS: 送信したワンタイムパスワード)', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'purpose';
EXEC sys.sp_addextendedproperty N'MS_Description', N'トークンまたはコードの SHA-256 ハッシュ (16 進。平文は保存しない)', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'token_hash';
EXEC sys.sp_addextendedproperty N'MS_Description', N'有効期限', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'expires_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'使用日時 (NULL は未使用)', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'used_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'無効化日時 (新しいトークンの発行などで無効にした日時)', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'revoked_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'照合に失敗した回数 (ワンタイムパスワード用。otp_max_attempts に達したら無効)', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'attempt_count';
EXEC sys.sp_addextendedproperty N'MS_Description', N'発行を要求した接続元IPアドレス', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'ip_address';
EXEC sys.sp_addextendedproperty N'MS_Description', N'発行日時', N'SCHEMA', N'dbo', N'TABLE', N'user_tokens', N'COLUMN', N'created_at';

EXEC sys.sp_addextendedproperty N'MS_Description', N'ログイン履歴 (ログインの試行。ログアウト・タイムアウトは user_sessions.end_reason に記録)', N'SCHEMA', N'dbo', N'TABLE', N'login_histories';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ログイン履歴ID', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーID (users.id。特定できない場合は NULL)', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'user_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'入力されたログインID (OIDC はメールアドレスまたは sub)', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'login_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'本人確認の方式 (PASSWORD / OIDC)', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'auth_method';
EXEC sys.sp_addextendedproperty N'MS_Description', N'成功フラグ', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'succeeded';
EXEC sys.sp_addextendedproperty N'MS_Description', N'失敗理由 (INVALID_PASSWORD / LOCKED / INACTIVE / NOT_FOUND / IP_BLOCKED / MFA_FAILED / SSO_FAILED)', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'failure_reason';
EXEC sys.sp_addextendedproperty N'MS_Description', N'接続元IPアドレス (X-Forwarded-For を考慮した値)', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'ip_address';
EXEC sys.sp_addextendedproperty N'MS_Description', N'User-Agent', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'user_agent';
EXEC sys.sp_addextendedproperty N'MS_Description', N'試行日時', N'SCHEMA', N'dbo', N'TABLE', N'login_histories', N'COLUMN', N'attempted_at';

EXEC sys.sp_addextendedproperty N'MS_Description', N'ロール', N'SCHEMA', N'dbo', N'TABLE', N'roles';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ロールID', N'SCHEMA', N'dbo', N'TABLE', N'roles', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ロールコード (例: ADMIN。[Authorize(Roles = ...)] で使う)', N'SCHEMA', N'dbo', N'TABLE', N'roles', N'COLUMN', N'code';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ロール名', N'SCHEMA', N'dbo', N'TABLE', N'roles', N'COLUMN', N'name';
EXEC sys.sp_addextendedproperty N'MS_Description', N'作成日時', N'SCHEMA', N'dbo', N'TABLE', N'roles', N'COLUMN', N'created_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'更新日時', N'SCHEMA', N'dbo', N'TABLE', N'roles', N'COLUMN', N'updated_at';

EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーロール', N'SCHEMA', N'dbo', N'TABLE', N'user_roles';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーID (users.id)', N'SCHEMA', N'dbo', N'TABLE', N'user_roles', N'COLUMN', N'user_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ロールID (roles.id)', N'SCHEMA', N'dbo', N'TABLE', N'user_roles', N'COLUMN', N'role_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'作成日時', N'SCHEMA', N'dbo', N'TABLE', N'user_roles', N'COLUMN', N'created_at';

EXEC sys.sp_addextendedproperty N'MS_Description', N'ASP.NET Core Data Protection の鍵 (削除すると Cookie と暗号化済みの TOTP シークレットが復号できなくなる)', N'SCHEMA', N'dbo', N'TABLE', N'data_protection_keys';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ID', N'SCHEMA', N'dbo', N'TABLE', N'data_protection_keys', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'鍵の名前', N'SCHEMA', N'dbo', N'TABLE', N'data_protection_keys', N'COLUMN', N'friendly_name';
EXEC sys.sp_addextendedproperty N'MS_Description', N'鍵の内容 (XML)', N'SCHEMA', N'dbo', N'TABLE', N'data_protection_keys', N'COLUMN', N'xml';
