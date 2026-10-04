# ログイン機能 設計書

作成日: 2026-10-04
対象: MyBlazorApp（Blazor Web App / .NET 10 / Interactive Server / MudBlazor / SageMaker Studio Code Editor のプロキシ配下）

ログイン機能について、要件・方式・テーブル定義の決定事項をまとめる。DB は PostgreSQL を前提とする（SQL Server 版は実装時に作成）。

---

## 1. 要件

### 1.1 画面・アクセス制御
- 画面は MudBlazor で作成する。
- ログインしないと、他の画面にアクセスできないようにする。
- ログインしなくても見られるのは次のものだけとする: ログイン画面、パスワードリセットの画面、`not-found`、`Error`、静的ファイル。

### 1.2 自動ログアウト
- **最後の操作から一定時間（`idle_timeout_minutes`）たったら**ログアウトする。
- 操作とみなすもの: クリック、キー入力、画面遷移、マウスの移動、スクロール。
- **1 つのタブで操作していれば、他のタブもログイン状態のままにする。**
- **ユーザーの操作で始まったサーバー側の処理が終わるまでは、ログアウトしない。** 対象は特定の処理に限らず、すべてのイベント処理とする。アイドル時間は「最後の操作」と「サーバー処理が終わった時刻」の新しい方から数える。
- **最大ログイン時間は設けない。**
- ログアウトする前に警告を出す（`timeout_warning_seconds`）。
- **プロキシのタイムアウトで接続（WebSocket）が切れても、ログイン状態を保つ。**

### 1.3 アカウントロック
- パスワードの誤りが規定の回数（`max_failed_attempts`）に達したら、一定時間（`lockout_minutes`）ロックする。
- IP アドレス単位でも試行回数を制限する。
- 管理者がロックを解除する画面は作らない。ロックは、時間がたつかパスワードをリセットすると解除される。

### 1.4 パスワードポリシー
- ユーザーを作成するときとパスワードを変更するときに、`security_policies` の設定を満たしているかチェックする。
- 過去 N 世代のパスワードは再利用できない。履歴は `users.password_history`（jsonb）に全件残す。

### 1.5 パスワードリセット（メール）
| 項目 | 決定事項 |
|---|---|
| リンクの有効期限 | テーブルの設定（`password_reset_token_minutes`）で決める |
| リンクの使用 | 1 回限り。新しく申請したら、それまでに発行したリンクは無効にする |
| 申請回数の制限 | 設けない |
| 未登録のメールアドレス | 登録されている場合と同じく「メールを送信しました」と表示する |
| 完了の通知メール | 送らない |
| リセット後の処理 | ロックを解除し、失敗回数を 0 にし、`security_stamp` を更新する（他の端末をログアウトさせる） |
| ロック中のアカウント | リセットできる |
| パスワードを持たないユーザー（SSO のみ） | リセットでパスワードを設定できる。設定後は ID・パスワードでもログインできる |
| メールの送信 | SMTP を使う。送信元のアドレスなどは `mail_settings` テーブルに持つ |
| `users.email` | 必須とし、大文字と小文字を区別せずに重複を禁止する |

### 1.6 ログイン履歴
- ログインの試行（成功と失敗）を `login_histories` に記録する。
- ログアウトとタイムアウトは `user_sessions.end_reason` に記録する。

### 1.7 将来の拡張（実装しやすい形にしておく）
| 項目 | 決定事項 |
|---|---|
| OIDC | 対応する。`users` にいない人は自動で登録し、`login_id` にはメールアドレスを使う（登録後は変更しない） |
| アカウントの紐付け | SSO の利用者と ID・パスワードの利用者を、同じアカウントとして紐付ける |
| SAML | いったん考慮しない |
| ワンタイムパスワード | 認証アプリ（TOTP）と、メール・SMS で送る方式の両方に対応する |
| パスワード | SSO の利用者は持たないので、`users.password_hash` は NULL を許可する |
| ログインの処理 | 「本人確認（パスワード、SSO）→ 追加の認証（OTP）→ セッションの発行」の段階に分ける |

### 1.8 設計の方針
- ASP.NET Core Identity は使わず、テーブルは自前で作る。パスワードのハッシュ化には `PasswordHasher<T>` だけを使う。
- **外部キーは設定しない**（メンテナンスが大変なため）。テーブル間の整合性はアプリ側で守る。
- 規定値はすべて DB（`security_policies`、`mail_settings`）に持つ。
- 暗号化には ASP.NET Core Data Protection を使い、鍵は DB（`data_protection_keys`）に保存する。

---

## 2. 方式

### 2.1 ログインの流れ
```
[ログイン画面 (MudBlazor・対話モード)]
   │ 1. 本人確認 (パスワード / OIDC)
   │    ・IP 単位の制限 → アカウントのロック → パスワードの照合
   │    ・login_histories に記録
   │ 2. 追加の認証が必要な場合 → user_tokens (MFA_PENDING) を発行 → OTP の入力画面 (将来)
   │ 3. user_tokens (SIGNIN_TICKET) を発行
   ▼ forceLoad で遷移
[サインイン用のエンドポイント (HTTP)]
   │ 4. チケットを確認 → user_sessions を作成 → Cookie を発行 (セッション ID を入れる)
   ▼
[元の画面 (ReturnUrl。相対パスだけを許可)]
```
- ログイン用の Cookie は、Interactive Server の画面（SignalR の通信）からは設定できない。そのため、画面は MudBlazor の対話モードで作り、Cookie の発行は 1 回限りのチケットを使って HTTP のエンドポイントで行う。
- 画面には、ログインに失敗した理由に関係なく「ログイン ID またはパスワードが正しくありません」と表示する（存在するアカウントを推測されないようにするため）。失敗の理由は `login_histories` に記録する。

### 2.1.1 パスワードの有効期限
- 有効期限（`password_expiry_days`）が 0 より大きい場合、最後にパスワードを設定してからの日数で判定する。
- **期限の `password_expiry_warning_days` 日前から**（画面の名前は「有効期限切れ警告表示日数」）: ログインの直後に「有効期限が近づいています。今すぐ変更しますか？」と確認する。確認は 1 日 1 回（日本時間の日付が変わるまで）だけで、表示した日時を `users.password_expiry_notified_at` に記録する（端末やブラウザを変えても同じ日は出ない）。
  - 「今すぐ変更する」: ログインしてパスワード変更画面（`change-password?reason=expiring`）を開く。
  - 「あとで変更する」: そのままログインする。
  - サインイン用チケットは、選んだ時点で発行する（確認を開いたまま 1 分のチケットの期限が切れないように）。
- **期限切れ**: ログインの代わりにパスワードの再設定画面へ移り、新しいパスワードを設定してからログインし直す（履歴の `reason` は `EXPIRED`）。

### 2.2 アカウントロック
1. `locked_until > now()` ならロック中として失敗にする。パスワードが正しくてもログインさせない。
2. `last_failed_login_at` から `failed_count_reset_minutes` 分以上たっていれば、失敗回数を 0 とみなす。
3. パスワードが間違っている場合:
   - `failed_login_count` に 1 を足し、`last_failed_login_at = now()` にする。
   - 失敗回数が `max_failed_attempts` に達したら、`locked_until = now() + lockout_minutes` にする。
   - 回数の加算は `UPDATE ... SET failed_login_count = failed_login_count + 1 ... RETURNING` の 1 文で行う（同時にログインを試みられても、失敗回数を数え落とさないため）。
4. パスワードが正しい場合は、`failed_login_count = 0`、`locked_until = NULL`、`last_login_at = now()` にする。

### 2.3 IP アドレス単位の制限
- `login_histories` から、同じ IP アドレスで `ip_failed_window_minutes` 分以内に失敗した件数を数える。件数が `ip_max_failed_attempts` 以上なら、`ip_block_minutes` 分の間ログインさせない（失敗の理由は `IP_BLOCKED`）。
- 今はプロキシがないため、実際にどのヘッダーが届くかは確認できていない。プロキシを入れる前提で、次のように作る。

**接続元の IP の決め方**
| 状況 | 使う IP |
|---|---|
| 信頼するプロキシが設定されていない（今の環境） | `Connection.RemoteIpAddress` をそのまま使う |
| 信頼するプロキシから届いたリクエスト | `X-Forwarded-For` の右端から順に見ていき、信頼するプロキシ以外で最初に出てきた IP を使う |
| 信頼するプロキシ以外から届いたリクエスト | `X-Forwarded-For` は無視し、`RemoteIpAddress` を使う |

- ASP.NET Core 標準の `UseForwardedHeaders` を使い、`KnownProxies` / `KnownNetworks` に信頼するプロキシを設定する。`ForwardLimit` は、プロキシの段数に合わせる。
- **信頼するプロキシ以外が付けた `X-Forwarded-For` は使わない。** 使ってしまうと、攻撃者がヘッダーを偽造して IP 単位の制限をすり抜けたり、他人の IP を名乗ってその人をブロックさせたりできるため。
- 信頼するプロキシの一覧は `appsettings.json` に持つ（`ForwardedHeaders:KnownProxies`、`ForwardedHeaders:KnownNetworks`、`ForwardedHeaders:ForwardLimit`）。ネットワークの構成によって環境ごとに値が変わり、起動時に読み込む設定であるため、DB の `security_policies` には入れない。
- `UseForwardedHeaders` は、パイプラインの最初に置く（リクエストのログ、プロキシのパス対応、認証よりも前）。
- プロキシを入れたら、`/diag` の `RemoteIp` と転送ヘッダーを見て、設定が正しいか確認する。
- プロキシが転送ヘッダーを付けず、全員が同じ IP に見える場合は、`security_policies.ip_limit_enabled = false` にして IP 単位の制限を止める（アカウント単位のロックは引き続き使える）。

### 2.4 セッションと自動ログアウト
- ログインしたら `user_sessions` に 1 行作り、その ID を Cookie に入れる。**ログイン状態は、Blazor の接続ではなく、この行で判定する。**
- Cookie には有効期限を付けない（ブラウザを閉じると消える）。期限切れの判定は DB だけで行う。
- すべてのタブが同じ行を使うので、どのタブで操作しても `last_activity_at` が更新される。

| 仕組み | 内容 |
|---|---|
| 操作の検知 | ブラウザ側でクリック、キー入力、画面遷移、マウスの移動、スクロールを検知し（`passive: true`）、最後に操作した時刻を記録する。サーバーへの通知は最大で 60 秒に 1 回にし、その間に操作があったときだけ送る。通知には「最後の操作から何秒たったか」を含め、サーバーは通知した時刻ではなく実際に操作した時刻を記録する。マウスの移動は、位置が変わったときだけ数える（ダイアログの表示などで起きる mousemove を除くため） |
| 処理中の検知 | 全画面の基底クラス `AppPageBase`（`Components/Pages/_Imports.razor` の `@inherits`）で、イベント処理（`IHandleEvent.HandleEventAsync`）と画面を開いたときの読み込み（`SetParametersAsync`）を包み、`await` の先まで終わるまで処理中として数える。メモリ上でセッションごとに実行中の処理数を数え、処理が 30 秒以上続いている間は、30 秒ごとに `processing_heartbeat_at` を更新する。30 秒以上かかった処理は、終わったときに終了時刻を書き込む |
| ログアウトの判定 | 実行中の処理がなく、かつ `max(last_activity_at, processing_heartbeat_at)` から `idle_timeout_minutes` 分たっていたらログアウトにする |
| 確認のタイミング | 画面を開いている間は 30 秒ごとに確認する。HTTP リクエストを受けたときは、Cookie を検証する処理で確認する |
| 警告 | `timeout_warning_seconds` 秒前にダイアログを出す。「延長」ボタンを押したら、操作したものとして扱う |
| その他のログアウト | パスワードの変更やリセット（`security_stamp` が変わったとき）、アカウントの無効化、管理者による強制ログアウト（`REVOKED`） |
| ブラウザを閉じた場合 | セッションの行は `ended_at` が NULL のまま残る。次にそのセッションを確認したとき、または定期的な掃除の処理で `IDLE_TIMEOUT` として終了させる |

- 処理中の判定を、件数ではなく日時の定期的な更新で行うのは、アプリが落ちたときに処理中の状態が残り続けないようにするためである。
- 異常時の扱い: アプリが落ちたら更新が止まり、通常どおりアイドル時間を数え始める。処理中にプロキシが接続を切っても、処理と更新はサーバー側で続く。
- 当初は `CircuitHandler.CreateInboundActivityHandler` を使う予定だったが、実際に動かすと、クリックのイベントを受け付けた時点（数ミリ秒）で完了し、非同期の処理の終了を待たなかった。そのため基底クラスで包む方式にした。
- `Components/Pages` の外に置いた画面や、`@inherits` で別の基底クラスを指定した画面は対象外になる。
- 操作の通知間隔（60 秒。警告の表示中は 1 秒）、処理中の更新間隔（30 秒）、ログアウトの確認間隔（15 秒。警告の表示中は 3 秒）は、コードの定数とする。

### 2.5 プロキシが接続を切ったときの対策
- ログイン状態は Cookie と DB にあるので、切断されても失われない。再接続するか画面を再読み込みすれば、ログインしたまま使える。
- `DisconnectedCircuitRetentionPeriod`（既定は 3 分）を延ばし、再接続したときに入力途中の内容や処理の結果が残るようにする。
- `ReconnectModal` は再接続を試し続けるようにする。接続を作り直すしかなくなった場合は、画面を自動で再読み込みする。
- SignalR が通信を送る間隔（`KeepAliveInterval`、既定は 15 秒）を、プロキシのタイムアウトより短くする。

### 2.6 パスワードリセット
1. メールアドレスを入力してもらう。登録されているかどうかに関係なく、同じ表示にする。
2. 登録されている場合は、それまでの `PASSWORD_RESET` のトークンを無効にし（`revoked_at`）、新しいトークンを発行する。DB には SHA-256 のハッシュだけを保存する。
3. `mail_settings.public_base_url` をもとにリセット用の URL を作り、SMTP でメールを送る。
4. リセットの画面では、トークンの有効期限と、使用済みや無効になっていないかを確認する。新しいパスワードがポリシーを満たしているか、再利用になっていないかもチェックする。
5. `password_hash` と `password_history`（`reason = 'PASSWORD_RESET'`）を更新し、`password_changed_at` と `security_stamp` も更新する。`failed_login_count = 0`、`locked_until = NULL` にし、トークンには `used_at` を記録する。
6. ログイン中のセッションを `PASSWORD_CHANGED` で終了させる。

### 2.7 パスワードの変更と履歴
- `password_history` には、新しいパスワードを設定するたびに先頭に追加し、件数は切り詰めずに全件残す。
- 再利用のチェックでは、先頭から `password_history_count` 件と照合する（現在のパスワードも含む）。
- 要素の形式:
  ```json
  { "hash": "AQAAAAIAAYag...", "changedAt": "2026-10-04T10:00:00+09:00", "changedBy": 1, "reason": "ADMIN_RESET" }
  ```
  - `changedBy`: 変更した人の `users.id`。システムが行った場合は `null`。
  - `reason`: `INITIAL` / `USER_CHANGE` / `ADMIN_RESET` / `PASSWORD_RESET` / `EXPIRED`
- 監査で変更の記録が必要になったら、取得用の SQL で出す（`jsonb_array_elements`、SQL Server では `OPENJSON`）。

### 2.8 OIDC（将来）
- `user_external_logins` で `(provider, provider_subject)` を `users` に紐付ける。
- 紐付けがない場合は、メールアドレスで既存のユーザーを探して紐付ける。ただし、ID プロバイダーが `email_verified = true` を返し、かつ設定で信頼すると指定したプロバイダーの場合だけとする（乗っ取りを防ぐため）。
- 既存のユーザーが見つからない場合は自動で登録する（`login_id = email`、`password_hash = NULL`）。そのメールアドレスが別のユーザーの `login_id` としてすでに使われていたら、`SSO_FAILED` とする。

### 2.9 ワンタイムパスワード（将来）
- TOTP: シークレットは Data Protection で暗号化し（用途: `MyBlazorApp.TotpSecret`）、`users.totp_secret_encrypted` に保存する。リカバリーコードは、ハッシュ化して `users.mfa_recovery_codes` に保存する。
- メール・SMS: コードを `user_tokens`（`MFA_EMAIL` / `MFA_SMS`）に発行する。有効期限は `otp_code_minutes`、入力できる回数は `otp_max_attempts` で決める。

### 2.10 この環境での注意点
| 項目 | 内容 |
|---|---|
| ログイン画面へのリダイレクト | サーバーは `PathBase` を使って URL を作る。`ProxyBasePath` が未設定（VS Code のデバッグ）だと、`/login` に飛ばされて 404 になる。`ProxyBasePath` を必ず設定するか、リダイレクトを相対 URL で返すようにする |
| 全体へのログイン必須の設定 | `FallbackPolicy` を設定し、`Routes.razor` を `AuthorizeRouteView` に変える。`MapStaticAssets` は除外しないと、ログイン画面の CSS が読み込まれない。`/diag` を公開するかは別途決める |
| SQL のログ | Development 環境では SQL のパラメータの値もログに出る。認証関係の SQL では、パスワードハッシュやトークンが出ないようにする |
| Data Protection の鍵 | 鍵をなくすと、全員がログアウトされ、TOTP のシークレットも元に戻せなくなる。鍵は DB に保存し、**古い鍵は削除しない** |
| `LogicCommon/Auth.cs` | DB 接続用のパスワードを暗号化するもので、元の値に戻せる方式のため、ユーザーのパスワードには使わない |
| IP アドレス単位の制限 | プロキシ配下では、全員が同じ IP に見える可能性がある。信頼するプロキシを設定したうえで `X-Forwarded-For` を使う（2.3 を参照）。`/diag` には全ヘッダーと `RemoteIp` を表示している |
| 戻り先の URL | 相対パスだけを許可する（外部サイトへ飛ばされないようにするため） |

---

## 3. テーブル一覧

| テーブル | 内容 |
|---|---|
| `security_policies` | セキュリティの規定値（id = 1 の 1 行のみ） |
| `mail_settings` | メールの設定（id = 1 の 1 行のみ） |
| `users` | ユーザー |
| `user_external_logins` | OIDC のアカウントとの紐付け |
| `user_sessions` | セッション（ログイン 1 回につき 1 行） |
| `user_tokens` | 1 回限り・期限付きのトークンとワンタイムパスワード |
| `login_histories` | ログインの試行の履歴 |
| `roles` | ロール |
| `user_roles` | ユーザーとロールの対応 |
| `data_protection_keys` | Data Protection の鍵 |

テーブル間の関係（外部キーは設定しない）:
```
users.id ─┬─ user_external_logins.user_id
          ├─ user_sessions.user_id
          ├─ user_tokens.user_id
          ├─ login_histories.user_id (NULL 可)
          └─ user_roles.user_id ── user_roles.role_id ─ roles.id
```

---

## 4. DDL（PostgreSQL）

- `gen_random_uuid()` は PostgreSQL 13 以降なら拡張機能なしで使える。12 以前の場合は、先に `CREATE EXTENSION pgcrypto;` を実行する。
- 外部キーがないので、どの順番で実行しても構わない。
- 最初の管理者ユーザーは、`PasswordHasher` で作ったハッシュが必要なため、アプリ側の初期データ作成で登録する。

```sql
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
```

---

## 5. 外部キーがないことによる、アプリ側の整合性の管理
- ユーザーは物理削除しない（`is_active = false` で無効にする）ので、存在しないユーザーを指す行が残る心配はない。
- ロールを削除するときは、同じトランザクションで `DELETE FROM user_roles WHERE role_id = ...` も実行する。
- `user_roles` や `user_external_logins` に行を追加する前に、ユーザーやロールが実際に存在するかを確認する。

---

## 6. 未確定・要確認の事項
| 項目 | 状態 |
|---|---|
| SMTP のパスワードの暗号化 | 既存の `Auth.EncryptStringToBytes_Aes` を使うことを提案中（SQL で直接設定できるため）。Data Protection に統一する場合は、暗号化した値を出力するコマンドを用意する |
| `X-Forwarded-For` | 今はプロキシがないため確認できない。プロキシが入る前提で作り（2.3 を参照）、プロキシを入れたときに `/diag` で確認する |
| `/diag` | ログインしなくても見られるようにするか、未定 |
| 非同期処理の捕捉 | 解決済み。`AppPageBase` で包む方式にし、100 秒の処理の間ログアウトしないこと、終了時刻からアイドル時間を数えることを確認した |
| SQL Server 版の DDL | 作成済み（`Ddl/CreateAuthTables_SQLServer.sql`）。timestamptz は `datetimeoffset` ではなく `datetime2`（UTC）にした（Entity が `DateTime` のため）。構文は ScriptDom で確認済みだが、SQL Server での実行は未確認 |

---

## 7. 実装の順番
1. DDL のファイル（`_PostgreSQL` 版と `_SQLServer` 版）、Entity、DbContext、`Sql/` 配下の SQL
2. 認証の基盤: Cookie 認証、`user_sessions`、ログインを必須にする設定、プロキシ配下でのリダイレクト対応、Data Protection の鍵の保存
3. ログイン画面: アカウントロック、IP 単位の制限、ログイン履歴の記録
4. 自動ログアウト: 操作の検知、処理中の判定、警告ダイアログ、再接続時の対応
5. パスワードリセット: SMTP でのメール送信
6. ユーザー作成と、パスワードポリシーのチェック
7. （将来）OIDC、ワンタイムパスワード

### 実装状況（2026-10-04）
2〜6 は実装済み。7 は未実装（テーブル、`LoginService` の段階分け、`user_tokens` の用途は用意してある）。

| 項目 | 状態 |
|---|---|
| DDL のファイル | `Ddl/CreateAuthTables_PostgreSQL.sql`（作成済みのテーブルと一致することを確認済み）、`Ddl/CreateAuthTables_SQLServer.sql` |
| SQL | `Sql/` 配下に作成済み。DB によって書き方が違う 5 ファイルは `_PostgreSQL` と `_SQLServer` の両方がある。SQL Server 版は構文の確認のみで、実行は未確認 |
| 最初の管理者 | ユーザーが 0 人のときだけ、`setup` 画面から登録できる（`ADMIN` ロールも作られる） |
| メール送信 | `mail_settings` が未登録の間は送られない（ERROR ログが出る）。画面の表示は変わらない |

主なファイル:
- `Logic/Authentication/`: ログイン・セッション・パスワードリセット・ユーザー作成の処理、Cookie 認証の設定、`account/signin`・`account/end-session` のエンドポイント
- `Components/AppPageBase.cs`: サーバー処理中の判定（全画面の基底クラス）
- `Components/Layout/SessionMonitor.razor`（`.razor.js`）: 操作の検知、警告、自動ログアウト
- `Components/Pages/Account/`: ログイン、パスワードを忘れた場合、パスワードの再設定・変更、最初の管理者の登録
- `Components/Pages/Users/`: ユーザー一覧・作成（`ADMIN` ロールのみ）
- `Components/Pages/Admin/PasswordPolicySettings.razor`: パスワードポリシー（有効期限を知らせる日数を含む）と自動ログアウトの時間（`idle_timeout_minutes` / `timeout_warning_seconds`）の変更（`ADMIN` ロールのみ。メニューも管理者にだけ表示）。パスワードポリシーは、これから設定するパスワードに使われ、有効期限は次のログインから判定に使われる。自動ログアウトの時間は、ログイン中のセッションにもすぐに使われる。変更した人と変更前後の値は INFO のログに残す
