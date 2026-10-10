-- ログイン機能のテーブル (PostgreSQL)。設計は docs/login-design.md
-- 外部キーは設定しない (テーブル間の整合性はアプリ側で守る)。テーブルはどの順番で作ってもよい
-- gen_random_uuid() は PostgreSQL 13 以降なら拡張機能なしで使える (12 以前は先に CREATE EXTENSION pgcrypto; を実行する)
-- 最初の管理者は、アプリの setup 画面から登録する (パスワードのハッシュをアプリで作る必要があるため)


CREATE TABLE weather_forecasts (
    id             SERIAL       PRIMARY KEY,
    date           DATE         NOT NULL,
    temperature_c  INTEGER      NOT NULL,
    summary        VARCHAR(50),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);



-- ============================================================
-- セキュリティポリシー (id = 1 の 1 行だけの設定テーブル)
-- ============================================================
CREATE TABLE security_policies (
    id                           smallint     PRIMARY KEY DEFAULT 1,

    -- アカウントロック
    lockout_enabled              boolean      NOT NULL DEFAULT true,
    max_failed_attempts          integer      NOT NULL DEFAULT 5,
    lockout_minutes              integer      NOT NULL DEFAULT 10,
    failed_count_reset_minutes   integer      NOT NULL DEFAULT 30,

    -- IP アドレス単位の試行制限
    ip_limit_enabled             boolean      NOT NULL DEFAULT true,
    ip_max_failed_attempts       integer      NOT NULL DEFAULT 20,
    ip_failed_window_minutes     integer      NOT NULL DEFAULT 10,
    ip_block_minutes             integer      NOT NULL DEFAULT 10,

    -- パスワードポリシー
    password_min_length          integer      NOT NULL DEFAULT 8,
    password_max_length          integer      NOT NULL DEFAULT 128,
    require_uppercase            boolean      NOT NULL DEFAULT false,
    require_lowercase            boolean      NOT NULL DEFAULT false,
    require_digit                boolean      NOT NULL DEFAULT false,
    require_symbol               boolean      NOT NULL DEFAULT false,
    min_char_types               integer      NOT NULL DEFAULT 3,
    disallow_login_id            boolean      NOT NULL DEFAULT true,
    password_history_count       integer      NOT NULL DEFAULT 3,
    password_expiry_days         integer      NOT NULL DEFAULT 0,
    password_expiry_warning_days integer      NOT NULL DEFAULT 7,

    -- パスワードリセット
    password_reset_token_minutes integer      NOT NULL DEFAULT 30,

    -- セッション
    idle_timeout_minutes         integer      NOT NULL DEFAULT 30,
    timeout_warning_seconds      integer      NOT NULL DEFAULT 60,

    -- ワンタイムパスワード (メール / SMS)
    otp_code_length              integer      NOT NULL DEFAULT 6,
    otp_code_minutes             integer      NOT NULL DEFAULT 5,
    otp_max_attempts             integer      NOT NULL DEFAULT 5,

    created_at                   timestamptz  NOT NULL DEFAULT now(),
    updated_at                   timestamptz  NOT NULL DEFAULT now(),

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
    CONSTRAINT ck_security_policies_expiry_warning  CHECK (password_expiry_warning_days >= 0),
    CONSTRAINT ck_security_policies_reset_token     CHECK (password_reset_token_minutes >= 1),
    CONSTRAINT ck_security_policies_idle_timeout    CHECK (idle_timeout_minutes >= 1),
    CONSTRAINT ck_security_policies_timeout_warning CHECK (timeout_warning_seconds >= 0
                                                       AND timeout_warning_seconds < idle_timeout_minutes * 60),
    CONSTRAINT ck_security_policies_otp_length      CHECK (otp_code_length BETWEEN 4 AND 10),
    CONSTRAINT ck_security_policies_otp_minutes     CHECK (otp_code_minutes >= 1),
    CONSTRAINT ck_security_policies_otp_attempts    CHECK (otp_max_attempts >= 1)
);

COMMENT ON TABLE  security_policies                              IS 'セキュリティポリシー (id = 1 の 1 行のみ)';
COMMENT ON COLUMN security_policies.id                           IS 'ID (常に 1)';
COMMENT ON COLUMN security_policies.lockout_enabled              IS 'アカウントロックを行うか';
COMMENT ON COLUMN security_policies.max_failed_attempts          IS 'ロックするまでの連続失敗回数';
COMMENT ON COLUMN security_policies.lockout_minutes              IS 'ロック時間 (分)';
COMMENT ON COLUMN security_policies.failed_count_reset_minutes   IS '最後の失敗からこの分数が経過したら失敗回数を 0 に戻す (0 はリセットしない)';
COMMENT ON COLUMN security_policies.ip_limit_enabled             IS 'IP アドレス単位の試行制限を行うか';
COMMENT ON COLUMN security_policies.ip_max_failed_attempts       IS '同一 IP からの失敗がこの回数に達したらブロック';
COMMENT ON COLUMN security_policies.ip_failed_window_minutes     IS '失敗回数を数える期間 (分)';
COMMENT ON COLUMN security_policies.ip_block_minutes             IS 'IP ブロック時間 (分)';
COMMENT ON COLUMN security_policies.password_min_length          IS 'パスワード最小文字数';
COMMENT ON COLUMN security_policies.password_max_length          IS 'パスワード最大文字数';
COMMENT ON COLUMN security_policies.require_uppercase            IS '英大文字を必須とするか';
COMMENT ON COLUMN security_policies.require_lowercase            IS '英小文字を必須とするか';
COMMENT ON COLUMN security_policies.require_digit                IS '数字を必須とするか';
COMMENT ON COLUMN security_policies.require_symbol               IS '記号を必須とするか';
COMMENT ON COLUMN security_policies.min_char_types               IS '英大文字・英小文字・数字・記号のうち最低何種類を含めるか (0 は制限なし)';
COMMENT ON COLUMN security_policies.disallow_login_id            IS 'ログインID を含むパスワードを禁止するか';
COMMENT ON COLUMN security_policies.password_history_count       IS '現在のパスワードを含む直近何世代の再利用を禁止するか (0 は制限なし)';
COMMENT ON COLUMN security_policies.password_expiry_days         IS 'パスワード有効期限 (日、0 は無期限)';
COMMENT ON COLUMN security_policies.password_expiry_warning_days IS 'パスワードの有効期限の何日前からログイン時に変更を勧めるか (0 は知らせない)';
COMMENT ON COLUMN security_policies.password_reset_token_minutes IS 'パスワードリセットリンクの有効期限 (分)';
COMMENT ON COLUMN security_policies.idle_timeout_minutes         IS '最後の操作 (またはサーバー処理の終了) からログアウトまでの時間 (分)';
COMMENT ON COLUMN security_policies.timeout_warning_seconds      IS 'ログアウト前に警告を出す秒数 (0 は警告なし)';
COMMENT ON COLUMN security_policies.otp_code_length              IS 'メール / SMS で送るワンタイムパスワードの桁数';
COMMENT ON COLUMN security_policies.otp_code_minutes             IS 'ワンタイムパスワードの有効期限 (分)';
COMMENT ON COLUMN security_policies.otp_max_attempts             IS 'ワンタイムパスワード 1 件あたりの入力可能回数';
COMMENT ON COLUMN security_policies.created_at                   IS '作成日時';
COMMENT ON COLUMN security_policies.updated_at                   IS '更新日時';

INSERT INTO security_policies (id) VALUES (1);

-- ============================================================
-- メール設定 (id = 1 の 1 行だけの設定テーブル)
-- ============================================================
CREATE TABLE mail_settings (
    id                      smallint     PRIMARY KEY DEFAULT 1,
    smtp_host               varchar(255) NOT NULL,
    smtp_port               integer      NOT NULL DEFAULT 587,
    smtp_security           varchar(10)  NOT NULL DEFAULT 'STARTTLS',
    smtp_user               varchar(255),
    smtp_password_encrypted varchar(512),
    from_address            varchar(256) NOT NULL,
    from_name               varchar(100),
    public_base_url         varchar(512) NOT NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_mail_settings_single_row CHECK (id = 1),
    CONSTRAINT ck_mail_settings_port       CHECK (smtp_port BETWEEN 1 AND 65535),
    CONSTRAINT ck_mail_settings_security   CHECK (smtp_security IN ('NONE', 'STARTTLS', 'SSL'))
);

COMMENT ON TABLE  mail_settings                         IS 'メール設定 (id = 1 の 1 行のみ)';
COMMENT ON COLUMN mail_settings.id                      IS 'ID (常に 1)';
COMMENT ON COLUMN mail_settings.smtp_host               IS 'SMTP サーバー';
COMMENT ON COLUMN mail_settings.smtp_port               IS 'SMTP ポート';
COMMENT ON COLUMN mail_settings.smtp_security           IS '暗号化方式 (NONE / STARTTLS / SSL)';
COMMENT ON COLUMN mail_settings.smtp_user               IS 'SMTP 認証ユーザー (認証なしは NULL)';
COMMENT ON COLUMN mail_settings.smtp_password_encrypted IS 'SMTP 認証パスワード (Auth.EncryptStringToBytes_Aes で暗号化)';
COMMENT ON COLUMN mail_settings.from_address            IS '送信元メールアドレス';
COMMENT ON COLUMN mail_settings.from_name               IS '送信元の表示名';
COMMENT ON COLUMN mail_settings.public_base_url         IS 'メール本文のリンクに使う URL (例: https://<Studio のドメイン>/codeeditor/default/proxy/5000/)';
COMMENT ON COLUMN mail_settings.created_at              IS '作成日時';
COMMENT ON COLUMN mail_settings.updated_at              IS '更新日時';

-- 値は環境に合わせて設定する
-- INSERT INTO mail_settings (smtp_host, smtp_port, smtp_security, from_address, from_name, public_base_url)
-- VALUES ('smtp.example.com', 587, 'STARTTLS', 'no-reply@example.com', 'MyBlazorApp', 'https://.../codeeditor/default/proxy/5000/');

-- ============================================================
-- ユーザー
-- ============================================================
CREATE TABLE users (
    id                    bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    login_id              varchar(256) NOT NULL,
    password_hash         varchar(255),
    password_history      jsonb        NOT NULL DEFAULT '[]'::jsonb,
    display_name          varchar(100) NOT NULL,
    email                 varchar(256) NOT NULL,
    phone_number          varchar(20),
    is_active             boolean      NOT NULL DEFAULT true,
    failed_login_count    integer      NOT NULL DEFAULT 0,
    last_failed_login_at  timestamptz,
    locked_until          timestamptz,
    last_login_at         timestamptz,
    password_changed_at   timestamptz,
    password_expiry_notified_at timestamptz,
    mfa_enabled           boolean      NOT NULL DEFAULT false,
    mfa_method            varchar(10),
    totp_secret_encrypted varchar(512),
    mfa_recovery_codes    jsonb        NOT NULL DEFAULT '[]'::jsonb,
    security_stamp        varchar(64)  NOT NULL DEFAULT gen_random_uuid()::text,
    created_at            timestamptz  NOT NULL DEFAULT now(),
    updated_at            timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_users_login_id                 UNIQUE (login_id),
    CONSTRAINT ck_users_failed_login_count       CHECK (failed_login_count >= 0),
    CONSTRAINT ck_users_password_history_array   CHECK (jsonb_typeof(password_history) = 'array'),
    CONSTRAINT ck_users_mfa_recovery_codes_array CHECK (jsonb_typeof(mfa_recovery_codes) = 'array'),
    CONSTRAINT ck_users_mfa_method               CHECK (mfa_method IN ('TOTP', 'EMAIL', 'SMS')),
    CONSTRAINT ck_users_mfa_enabled              CHECK (NOT mfa_enabled OR mfa_method IS NOT NULL),
    CONSTRAINT ck_users_mfa_totp                 CHECK (mfa_method <> 'TOTP' OR totp_secret_encrypted IS NOT NULL),
    CONSTRAINT ck_users_mfa_sms                  CHECK (mfa_method <> 'SMS'  OR phone_number IS NOT NULL)
);

CREATE UNIQUE INDEX uq_users_email ON users (lower(email));

COMMENT ON TABLE  users                       IS 'ユーザー';
COMMENT ON COLUMN users.id                    IS 'ユーザーID';
COMMENT ON COLUMN users.login_id              IS 'ログインID (SSO で自動登録したユーザーは登録時のメールアドレス。以後変更しない)';
COMMENT ON COLUMN users.password_hash         IS 'パスワードハッシュ (PasswordHasher の出力。ソルトを含む。SSO のみの利用者は NULL)';
COMMENT ON COLUMN users.password_history      IS 'パスワード設定履歴 (全件・新しい順。先頭が現在のパスワード。要素: {"hash","changedAt","changedBy","reason"})';
COMMENT ON COLUMN users.display_name          IS '表示名';
COMMENT ON COLUMN users.email                 IS 'メールアドレス (大文字小文字を区別せず一意。パスワードリセット・SSO の紐付けに使用)';
COMMENT ON COLUMN users.phone_number          IS '電話番号 (SMS によるワンタイムパスワード用、E.164 形式)';
COMMENT ON COLUMN users.is_active             IS '有効フラグ (false で無効化。物理削除はしない)';
COMMENT ON COLUMN users.failed_login_count    IS '連続ログイン失敗回数';
COMMENT ON COLUMN users.last_failed_login_at  IS '最後にログインに失敗した日時';
COMMENT ON COLUMN users.locked_until          IS 'ロックアウト解除日時 (この日時まではログイン不可)';
COMMENT ON COLUMN users.last_login_at         IS '最終ログイン日時';
COMMENT ON COLUMN users.password_changed_at   IS 'パスワード変更日時 (有効期限の判定用)';
COMMENT ON COLUMN users.password_expiry_notified_at IS '有効期限切れの警告ダイアログを最後に表示した日時 (警告は 1 日 1 回だけ表示する)';
COMMENT ON COLUMN users.mfa_enabled           IS '追加認証 (ワンタイムパスワード) を行うか';
COMMENT ON COLUMN users.mfa_method            IS '追加認証の方式 (TOTP / EMAIL / SMS)';
COMMENT ON COLUMN users.totp_secret_encrypted IS 'TOTP のシークレット (ASP.NET Core Data Protection で暗号化。用途: MyBlazorApp.TotpSecret)';
COMMENT ON COLUMN users.mfa_recovery_codes    IS 'TOTP のリカバリーコード (ハッシュ化した値の配列。使用済みは削除)';
COMMENT ON COLUMN users.security_stamp        IS 'セキュリティスタンプ (パスワード変更・リセット時に更新し、既存のセッションを無効化する)';
COMMENT ON COLUMN users.created_at            IS '作成日時';
COMMENT ON COLUMN users.updated_at            IS '更新日時';

-- ============================================================
-- 外部ログイン (OIDC のアカウントと users の紐付け)
-- ============================================================
CREATE TABLE user_external_logins (
    id               bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id          bigint       NOT NULL,
    provider         varchar(50)  NOT NULL,
    provider_subject varchar(255) NOT NULL,
    email_at_link    varchar(256),
    linked_at        timestamptz  NOT NULL DEFAULT now(),
    last_login_at    timestamptz,
    CONSTRAINT uq_user_external_logins_provider_subject UNIQUE (provider, provider_subject)
);

CREATE INDEX ix_user_external_logins_user_id ON user_external_logins (user_id);

COMMENT ON TABLE  user_external_logins                  IS '外部ログイン (OIDC のアカウントと users の紐付け。1 ユーザーに複数可)';
COMMENT ON COLUMN user_external_logins.id               IS '外部ログインID';
COMMENT ON COLUMN user_external_logins.user_id          IS 'ユーザーID (users.id)';
COMMENT ON COLUMN user_external_logins.provider         IS 'プロバイダー名 (設定ファイルの名前。例: EntraID)';
COMMENT ON COLUMN user_external_logins.provider_subject IS 'プロバイダー側のユーザー識別子 (OIDC の sub クレーム)';
COMMENT ON COLUMN user_external_logins.email_at_link    IS '紐付けたときのメールアドレス (監査用)';
COMMENT ON COLUMN user_external_logins.linked_at        IS '紐付け日時';
COMMENT ON COLUMN user_external_logins.last_login_at    IS 'このプロバイダーでの最終ログイン日時';

-- ============================================================
-- ユーザーセッション
-- ============================================================
CREATE TABLE user_sessions (
    id                      uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 bigint       NOT NULL,
    auth_method             varchar(20)  NOT NULL DEFAULT 'PASSWORD',
    mfa_method              varchar(20),
    logged_in_at            timestamptz  NOT NULL DEFAULT now(),
    last_activity_at        timestamptz  NOT NULL DEFAULT now(),
    processing_heartbeat_at timestamptz,
    ended_at                timestamptz,
    end_reason              varchar(20),
    ip_address              varchar(45),
    user_agent              varchar(512),
    CONSTRAINT ck_user_sessions_auth_method CHECK (auth_method IN ('PASSWORD', 'OIDC')),
    CONSTRAINT ck_user_sessions_mfa_method  CHECK (mfa_method IN ('TOTP', 'EMAIL', 'SMS', 'RECOVERY_CODE')),
    CONSTRAINT ck_user_sessions_end_reason  CHECK (end_reason IN ('LOGOUT', 'IDLE_TIMEOUT', 'REVOKED', 'PASSWORD_CHANGED')),
    CONSTRAINT ck_user_sessions_ended
        CHECK ((ended_at IS NULL AND end_reason IS NULL) OR (ended_at IS NOT NULL AND end_reason IS NOT NULL))
);

CREATE INDEX ix_user_sessions_user_id ON user_sessions (user_id);
CREATE INDEX ix_user_sessions_active  ON user_sessions (user_id) WHERE ended_at IS NULL;

COMMENT ON TABLE  user_sessions                         IS 'ユーザーセッション (ログイン 1 回につき 1 行。全タブで共有)';
COMMENT ON COLUMN user_sessions.id                      IS 'セッションID (Cookie に格納)';
COMMENT ON COLUMN user_sessions.user_id                 IS 'ユーザーID (users.id)';
COMMENT ON COLUMN user_sessions.auth_method             IS '本人確認の方式 (PASSWORD / OIDC)';
COMMENT ON COLUMN user_sessions.mfa_method              IS '追加認証の方式 (TOTP / EMAIL / SMS / RECOVERY_CODE。追加認証なしは NULL)';
COMMENT ON COLUMN user_sessions.logged_in_at            IS 'ログイン日時';
COMMENT ON COLUMN user_sessions.last_activity_at        IS '最終操作日時 (クリック・キー入力・画面遷移・マウス移動・スクロール。どのタブの操作でも更新)';
COMMENT ON COLUMN user_sessions.processing_heartbeat_at IS 'サーバー処理の生存通知・終了日時 (処理中は定期的に更新し、終了時に終了日時を記録。アイドル時間は last_activity_at とこの値の新しい方から数える)';
COMMENT ON COLUMN user_sessions.ended_at                IS 'セッション終了日時 (NULL はログイン中)';
COMMENT ON COLUMN user_sessions.end_reason              IS '終了理由 (LOGOUT / IDLE_TIMEOUT / REVOKED / PASSWORD_CHANGED)';
COMMENT ON COLUMN user_sessions.ip_address              IS 'ログイン時の接続元IPアドレス';
COMMENT ON COLUMN user_sessions.user_agent              IS 'ログイン時の User-Agent';

-- ============================================================
-- ユーザートークン (1 回限り・期限付きのトークンとコード)
-- ============================================================
CREATE TABLE user_tokens (
    id            bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id       bigint       NOT NULL,
    purpose       varchar(20)  NOT NULL,
    token_hash    varchar(64)  NOT NULL,
    expires_at    timestamptz  NOT NULL,
    used_at       timestamptz,
    revoked_at    timestamptz,
    attempt_count integer      NOT NULL DEFAULT 0,
    ip_address    varchar(45),
    created_at    timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_user_tokens_purpose
        CHECK (purpose IN ('PASSWORD_RESET', 'SIGNIN_TICKET', 'MFA_PENDING', 'MFA_EMAIL', 'MFA_SMS')),
    CONSTRAINT ck_user_tokens_attempt_count CHECK (attempt_count >= 0)
);

CREATE INDEX ix_user_tokens_token_hash      ON user_tokens (token_hash);
CREATE INDEX ix_user_tokens_user_id_purpose ON user_tokens (user_id, purpose) WHERE used_at IS NULL AND revoked_at IS NULL;
CREATE INDEX ix_user_tokens_expires_at      ON user_tokens (expires_at);

COMMENT ON TABLE  user_tokens               IS 'ユーザートークン (1 回限り・期限付きのトークンとワンタイムパスワード)';
COMMENT ON COLUMN user_tokens.id            IS 'トークンID';
COMMENT ON COLUMN user_tokens.user_id       IS 'ユーザーID (users.id)';
COMMENT ON COLUMN user_tokens.purpose       IS '用途 (PASSWORD_RESET: リセットリンク / SIGNIN_TICKET: 画面からサインイン処理への受け渡し / MFA_PENDING: 本人確認済み・追加認証待ち / MFA_EMAIL, MFA_SMS: 送信したワンタイムパスワード)';
COMMENT ON COLUMN user_tokens.token_hash    IS 'トークンまたはコードの SHA-256 ハッシュ (16 進。平文は保存しない)';
COMMENT ON COLUMN user_tokens.expires_at    IS '有効期限';
COMMENT ON COLUMN user_tokens.used_at       IS '使用日時 (NULL は未使用)';
COMMENT ON COLUMN user_tokens.revoked_at    IS '無効化日時 (新しいトークンの発行などで無効にした日時)';
COMMENT ON COLUMN user_tokens.attempt_count IS '照合に失敗した回数 (ワンタイムパスワード用。otp_max_attempts に達したら無効)';
COMMENT ON COLUMN user_tokens.ip_address    IS '発行を要求した接続元IPアドレス';
COMMENT ON COLUMN user_tokens.created_at    IS '発行日時';

-- ============================================================
-- ログイン履歴
-- ============================================================
CREATE TABLE login_histories (
    id             bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id        bigint,
    login_id       varchar(256) NOT NULL,
    auth_method    varchar(20)  NOT NULL DEFAULT 'PASSWORD',
    succeeded      boolean      NOT NULL,
    failure_reason varchar(20),
    ip_address     varchar(45),
    user_agent     varchar(512),
    attempted_at   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_login_histories_auth_method
        CHECK (auth_method IN ('PASSWORD', 'OIDC')),
    CONSTRAINT ck_login_histories_failure_reason
        CHECK (failure_reason IN ('INVALID_PASSWORD', 'LOCKED', 'INACTIVE', 'NOT_FOUND', 'IP_BLOCKED', 'MFA_FAILED', 'SSO_FAILED')),
    CONSTRAINT ck_login_histories_result
        CHECK ((succeeded AND failure_reason IS NULL) OR (NOT succeeded AND failure_reason IS NOT NULL))
);

CREATE INDEX ix_login_histories_attempted_at      ON login_histories (attempted_at);
CREATE INDEX ix_login_histories_user_id_attempted ON login_histories (user_id, attempted_at);
CREATE INDEX ix_login_histories_ip_failed         ON login_histories (ip_address, attempted_at) WHERE NOT succeeded;

COMMENT ON TABLE  login_histories                IS 'ログイン履歴 (ログインの試行。ログアウト・タイムアウトは user_sessions.end_reason に記録)';
COMMENT ON COLUMN login_histories.id             IS 'ログイン履歴ID';
COMMENT ON COLUMN login_histories.user_id        IS 'ユーザーID (users.id。特定できない場合は NULL)';
COMMENT ON COLUMN login_histories.login_id       IS '入力されたログインID (OIDC はメールアドレスまたは sub)';
COMMENT ON COLUMN login_histories.auth_method    IS '本人確認の方式 (PASSWORD / OIDC)';
COMMENT ON COLUMN login_histories.succeeded      IS '成功フラグ';
COMMENT ON COLUMN login_histories.failure_reason IS '失敗理由 (INVALID_PASSWORD / LOCKED / INACTIVE / NOT_FOUND / IP_BLOCKED / MFA_FAILED / SSO_FAILED)';
COMMENT ON COLUMN login_histories.ip_address     IS '接続元IPアドレス (X-Forwarded-For を考慮した値)';
COMMENT ON COLUMN login_histories.user_agent     IS 'User-Agent';
COMMENT ON COLUMN login_histories.attempted_at   IS '試行日時';

-- ============================================================
-- ロール
-- ============================================================
CREATE TABLE roles (
    id         bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code       varchar(50)  NOT NULL,
    name       varchar(100) NOT NULL,
    created_at timestamptz  NOT NULL DEFAULT now(),
    updated_at timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_roles_code UNIQUE (code)
);

COMMENT ON TABLE  roles            IS 'ロール';
COMMENT ON COLUMN roles.id         IS 'ロールID';
COMMENT ON COLUMN roles.code       IS 'ロールコード (例: ADMIN。[Authorize(Roles = ...)] で使う)';
COMMENT ON COLUMN roles.name       IS 'ロール名';
COMMENT ON COLUMN roles.created_at IS '作成日時';
COMMENT ON COLUMN roles.updated_at IS '更新日時';

-- ============================================================
-- ユーザーロール
-- ============================================================
CREATE TABLE user_roles (
    user_id    bigint      NOT NULL,
    role_id    bigint      NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_user_roles PRIMARY KEY (user_id, role_id)
);

CREATE INDEX ix_user_roles_role_id ON user_roles (role_id);

COMMENT ON TABLE  user_roles            IS 'ユーザーロール';
COMMENT ON COLUMN user_roles.user_id    IS 'ユーザーID (users.id)';
COMMENT ON COLUMN user_roles.role_id    IS 'ロールID (roles.id)';
COMMENT ON COLUMN user_roles.created_at IS '作成日時';

-- ============================================================
-- Data Protection の鍵 (Cookie・TOTP シークレットの暗号化鍵)
-- ============================================================
CREATE TABLE data_protection_keys (
    id            integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    friendly_name text,
    xml           text
);

COMMENT ON TABLE  data_protection_keys               IS 'ASP.NET Core Data Protection の鍵 (削除すると Cookie と暗号化済みの TOTP シークレットが復号できなくなる)';
COMMENT ON COLUMN data_protection_keys.id            IS 'ID';
COMMENT ON COLUMN data_protection_keys.friendly_name IS '鍵の名前';
COMMENT ON COLUMN data_protection_keys.xml           IS '鍵の内容 (XML)';
