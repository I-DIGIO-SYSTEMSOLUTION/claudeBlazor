# CLAUDE.md

このファイルは、このリポジトリで作業する Claude Code (claude.ai/code) 向けのガイドです。

## 概要

既定のテンプレートをもとにした Blazor Web App（.NET 10、Interactive Server レンダーモード）です。**SageMaker Studio Code Editor** 上で動かし、そのポートプロキシ（`https://<Studio のドメイン>/codeeditor/default/proxy/5000/`）経由でアクセスできるようにカスタマイズしています。テンプレート以外のコードの大半は、パスにプレフィックスが付くこのプロキシの配下で Blazor を動かすためのものです。コード内のコメントは日本語です。

## コマンド

```bash
dotnet build -c Debug MyBlazorApp.csproj
dotnet run --launch-profile http        # http://localhost:5000 で待ち受け、ProxyBasePath を設定
dotnet test MyBlazorApp.Tests          # テストを実行（リポジトリ直下で）
scripts/reset-today.sh                 # 今日の打刻を消す（動作確認のやり直し用。リポジトリ直下で）
```

- dotnet は apt でインストールしてあり、本体は `/usr/bin/dotnet`、`DOTNET_ROOT=/usr/lib/dotnet` です。`.vscode/tasks.json` と `launch.json` はこれらのパスを直接書いています。
- VS Code のデバッグ（`.vscode/launch.json`）は `bin/Debug/net10.0/MyBlazorApp.dll` を直接起動します。そのため `Properties/launchSettings.json` は**適用されません**（待ち受けアドレスは launch.json 側で `ASPNETCORE_URLS=http://localhost:5000` を設定）。この起動方法では `ProxyBasePath` が未設定なので、`App.razor` 内のクライアント側 base href スクリプトによって動作しています。
- テストは xUnit のプロジェクト `MyBlazorApp.Tests/`（`MyBlazorApp.csproj` を参照）にあり、リポジトリ直下で `dotnet test MyBlazorApp.Tests` で実行する（引数なしの `dotnet test` は直下の `MyBlazorApp.csproj` が対象になり、テストは実行されない）。テストはデータベースを使わない。`MyBlazorApp.csproj` は `MyBlazorApp.Tests/**` を取り込まない設定にしてある。linter はない。

## 作業ルール

- 依頼された範囲以外のコードは変更しない。
- 不具合の原因は、今のコードを読んで調べる。git の履歴（`git log`・`git show`・過去のコミットとの差分）やほかのブランチ（`origin/main` など）は調べない（ユーザーが頼んだときを除く）。作業中の変更を `git status` / `git diff` で見るのはよい。
- 依頼と関係のない不具合に気づいても、ユーザーが不具合を探しているとき（「ほかに不具合がないか見て」などと頼まれたとき）だけ伝える。
- 未回答の `[Answer]` が残っている間は、その仕様に関わる実装をしない。
- 方針を人が承認するまで実装しない（ユーザーが「承認はいらない」「すぐ直して」などと頼んだときを除く）。
- 判定や入力チェックは C# のクラスに書く（画面や SQL だけに書かない）。
- テストコード（xUnit）は、ユーザーが依頼したときだけ書く。テストが失敗したら、テストではなく実装を直す（テストを変えるのは仕様が変わったときだけで、変える前に人に確認する）。
- コミットや push はしない（ユーザーが明示的に依頼した場合を除く）。
- 動作の確認は、確認項目表（`check-NN.md`）をもとにユーザーが画面で行う。データベースを読み書きする確認用のプログラムやスクリプトは作らない（ユーザーが頼んだときを除く）。
- 打刻のリセット（やり直し）を頼まれたら `scripts/reset-today.sh` を実行し、「今日の打刻をリセットしました」とだけ伝える（仕組みの説明は、聞かれたときだけでよい）。
- データベースを直接操作するときは、接続情報（パスワードなど）を画面やファイルに出さない。接続設定ファイル（`Setting/databaseCommonConnectionSetting.xml`）は変更しない。
- テーブル定義（`Ddl/` と、データベースのテーブル）は変更しない。
- パッケージの脆弱性の警告（NU1902 / NU1903）と `SYSLIB0001`（UTF-7）の警告には対応しない。

## 作業ファイル（ドキュメント）

GitHub の Issue・プルリクエストは使わない。Issue や仕様の記録は、次の md ファイルで管理する。

- ひな形は `docs/templates/` にある（`issue.md`、`check.md`）。ひな形そのものは書き換えず、コピーして使う。
- 機能追加は `docs/feature/` に置く。
  - `qa-NN.md`：要件の [Question] / [Answer]、確定した仕様、変更方針、変更記録。
  - `check-NN.md`：画面で確かめる確認項目表（`docs/templates/check.md` をコピーして作る）。
- 不具合は `docs/issues/` に置く。
  - `issue-NN.md`：Issue（`docs/templates/issue.md` をコピーして作る）。
  - `check-NN.md`：その Issue の確認項目表。
- `NN` はフォルダごとに 01 から振る 2 桁の連番。
  - 新しく作るときは、そのフォルダにある一番大きい番号の次にする（`docs/feature/` なら `qa-NN.md`、`docs/issues/` なら `issue-NN.md` の番号で数える）。
  - 同じ作業のファイルは同じ番号にする（`qa-01.md` と `check-01.md`、`issue-01.md` と `check-01.md`）。
  - 今あるファイルは上書きしない。ユーザーが番号を書かずに「新しく作って」と頼んだときも、この決め方で番号を振る。
- ユーザーが「Issue を作って」と言ったときは、GitHub ではなく `docs/issues/` に md ファイルを作る。
- `qa-NN.md` の書き方：
  - 質問は `[Question] Q1: …` の形で、選択肢とおすすめを付け、その下に空の `[Answer]` を置く。`[Answer]` の中身はユーザーが書くので、AI は書かない。
  - 確定した仕様は `## 仕様`、変更方針は `## 変更方針`、変更記録は `## 変更記録` の見出しの下に追記する。
- `check-NN.md` の確認項目は、仕様の要点に絞る（ユーザーが件数を指定しなければ 8 件まで）。「結果」の列はユーザーが画面で試して書くので、AI は空のままにする。
- 不具合の「失敗するテスト（Red）」を頼まれたら、テストケースは AI が決めて書く。元にするのは、対象の Issue（`issue-NN.md` の「期待する動作」）と、同じ番号の `check-NN.md` の NG ケース（空なら Issue だけでよい）。「このバグ」などと言われたら、直前に扱っていた Issue を対象にする。書いたら、どのケースをテストにしたか（テストにできないケースはその理由も）を一覧で伝える。実装は変えない。
- `docs/notes/reflection.md` はユーザーが自分で書く振り返りメモなので、AI は書かない・変更しない。

## プロキシ対応（Program.cs と App.razor にまたがる）

SageMaker のプロキシは `/codeeditor/default/proxy/5000` のプレフィックスを除去する場合としない場合があるため、両方に対応しています。

1. **サーバー側（`Program.cs`）**: 設定 `ProxyBasePath` があれば、ミドルウェアが `Request.Path` からプレフィックスを（付いていれば）取り除き、`Request.PathBase` を常に設定します。書き換え後のパスでルーティングさせるため、`app.UseRouting()` をこのミドルウェアの*後*で明示的に呼んでいます（`ProxyBasePath` がある場合の分岐内のみ）。この順序は崩さないでください。
2. **サーバーが出力する `<base href>`（`Components/App.razor`）**: `Request.PathBase` から設定します。
3. **クライアント側のフォールバック（`App.razor` のインラインスクリプト）**: `location.pathname` からサーバーが受け取った `Request.Path` を差し引いてプレフィックスを求め、`<base href>` を上書きします。また、ルート URL の末尾に `/` を強制的に付け、相対 URL（`_framework/blazor.web.js`、SignalR の `_blazor` negotiate）がプロキシのプレフィックス配下で解決されるようにしています。そのため、静的アセットやナビゲーションのリンクは**相対パス**（先頭に `/` を付けない）のままにする必要があります。

診断用:
- `GET /diag`（Program.cs）— Blazor の接続がなくても表示できるプレーンな HTML ページです。PathBase / Path / RawTarget / ヘッダーを表示し、ブラウザから `blazor.web.js`、`app.css`、`_blazor/negotiate` の取得を試します。
- リクエストログ用のミドルウェアが、すべてのリクエストについて `[REQ] <method> <path> -> <status>` をログに出します。

## 注意点

- UI は MudBlazor（Bootstrap は読み込んでいない）。`MudThemeProvider` などのプロバイダーは `MainLayout.razor` に置いている。メニューは `NavMenu.razor` の `MudMenu`（AppBar 右上のドロップダウン）で、`MudMenuItem` の `Href` も相対パス（先頭に `/` を付けない、ホームは `./`）にすること。テーマは `AppThemes.cs` に定義し、AppBar の `ThemeMenu` で切り替える（選択とダークモードは Cookie `MyBlazorApp.Theme` / `MyBlazorApp.DarkMode` に保存し、`MainLayout` がプリレンダリング時に読んで `[PersistentState]` で対話モードへ引き継ぐ。初期表示から選んだ色になる）。
- パッケージの静的アセット（`_content/MudBlazor/...`）は、Development 環境か publish 後でないと配信されない（Production 環境で `bin/` の dll を直接起動すると 500 になる）。
- SQL 文は `Sql/` 配下の `*.sql` に書き、`SqlStatementStore`（シングルトン）が起動時にメモリへ読み込み、ファイル変更を検知して読み直す。キーは `Sql/` からの相対パスで拡張子なし（例: `WeatherForecast/Select`）。パラメータは `{0}` 形式で `ExecuteSqlRaw` / `FromSqlRaw` に渡す（SQL 文全体が `string.Format` で処理されるので、コメント内も含めて `{` `}` は `{{` `}}` にしないと FormatException になる）。監視するのはコンテンツルート（= 起動時のカレントディレクトリ）の `Sql/` で、`bin/` 側ではない。
- 条件が可変な SQL は `SqlStatementStore.Build(name, new { ... })` を使う（`LogicCommon/SqlTemplate.cs`）。`--#if 名前` 〜 `--#endif` の行は引数が指定されたとき（null・空文字・空コレクション以外）だけ残り、`@名前` は位置パラメータに置き換わる（コレクションは IN 句用に要素数分へ展開。要素が配列などのコレクションなら `(…), (…)` の行に展開するので、`VALUES @rows` で複数行をまとめて渡せる。例: `Sql/WeatherForecast/Update_PostgreSQL.sql` / `Update_SQLServer.sql` の BULK UPDATE）。条件を外しても成り立つよう `WHERE 1 = 1` にしておく。例: `Sql/WeatherForecast/Search.sql`。
- DB ごとに SQL が異なる場合は `Search_PostgreSQL.sql` / `Search_SQLServer.sql` のようにファイル名の末尾に `_<DB 種別>`（`databaseType` の値）を付ける。`Get` / `Build` には DB 種別なしの名前を渡し、DB 用のファイルがあればそれを、無ければ共通の `Search.sql` を使う。`_` は DB 種別の区切り専用で、`_` の後ろが DB 種別でないファイル（綴り間違いを含む）はエラーログ（log4net の ERROR ファイルにも出力）を出して読み込まない。
- ログは 2 系統ある。ASP.NET Core 標準の `ILogger`（コンソール出力）と、LogicCommon 配下が使う log4net（`log4net.config`、出力先は `<コンテンツルート>/logs/yyyyMMdd_app_<DEBUG|INFO|WARN|ERROR>.log` で日付・レベルごとに分かれる、Program.cs で `ConfigureAndWatch`）。EF Core が実行した SQL は `DatabaseConnectionHelper.UseDatabase` の `LogTo` で log4net に DEBUG で出し、Development のときだけパラメータの値も出す。
- csproj で `BlazorDisableThrowNavigationException` を有効にしています。404 は `UseStatusCodePagesWithReExecute("/not-found")` と `Router` の `NotFoundPage` で処理します。
- `SqlTemplate` に渡した null は null のまま EF Core に渡す（`DBNull.Value` にすると `ExecuteSqlRaw` で例外になる）。

## ログイン機能

設計と決定事項は `docs/login-design.md`。処理は `Logic/Authentication/`、テーブルは `users` / `user_sessions` / `user_tokens` / `login_histories` / `security_policies` など（外部キーは設定しない方針）。テーブルの DDL は `Ddl/CreateAuthTables_<DB 種別>.sql`（`Ddl/` はアプリが読み込む `Sql/` とは別のフォルダ）。リリース前の開発中なので、列を追加・変更したときは変更用の SQL は作らず（ただし今は作業ルールのとおりテーブル定義を変更しない）、`CreateAuthTables`（PostgreSQL 版と SQL Server 版）と `docs/login-design.md` の DDL を直接直す（開発用の DB には ALTER 文を手で流す）。SQL Server では日時を `datetime2` に UTC で持つ（Entity が `DateTime` のため）。

- `Components/_Imports.razor` の `@attribute [Authorize]` で全画面がログイン必須。ログインなしで見せる画面には `@attribute [AllowAnonymous]` を付ける。
- ログイン Cookie は対話モード（SignalR）では発行・削除できないため、`account/signin`（チケットで発行）と `account/end-session` の HTTP エンドポイントで行う。画面からは `NavigateTo(..., forceLoad: true)` で移る（`Href` だと画面内の遷移になり 404）。アプリの URL に `logout` を含めないこと（SageMaker のプロキシ配下でアプリに届かず、SageMaker の認証が切れる現象があった）。
- サーバーが返すリダイレクトは `AppUrl.RelativeFromRequest` で相対パスにする（`ProxyBasePath` 未設定だと `PathBase` が空で、絶対パスではプロキシのプレフィックスが抜けるため）。`UseAuthentication` / `UseAuthorization` はプロキシのパス書き換えの後に明示的に呼んでいる。
- ログイン状態は Blazor の接続ではなく `user_sessions` の行で判定する（自動ログアウトは `SessionMonitor`、HTTP リクエストは Cookie の `OnValidatePrincipal`）。
- 画面（`Components/Pages` 配下）は `Components/Pages/_Imports.razor` の `@inherits AppPageBase` で、イベント処理と初期表示の読み込みを「サーバー処理中」として数え、その間は自動ログアウトしない。別の `@inherits` を書くと対象外になる。`CircuitHandler.CreateInboundActivityHandler` は非同期処理の終了を待たないので、この用途には使えない。
- 対話モードでは `HttpContext` が使えないので、接続元 IP は `MainLayout` がプリレンダリング時に取得して `ClientConnectionInfo`（scoped）に入れている。プリレンダリングで取得した値を対話モードに引き継ぐ画面は `[PersistentState]` を使う（使わないと切り替え時に読み込み中の表示に戻り、ボタンが作り直される）。
- 認証関係の DbContext（`DbContextAuth` / `DbContextDataProtection`）は、開発時でも SQL のログにパラメータの値を出さない（`ConfigureDbContext(options, allowSensitiveDataLogging: false)`）。
- Data Protection の鍵は `data_protection_keys` テーブルに保存している。削除するとログイン Cookie と暗号化済みの値が復号できなくなる。
- 接続元 IP をプロキシの `X-Forwarded-For` から取るのは、`appsettings.json` の `ForwardedHeaders` に信頼するプロキシを書いたときだけ。

## 勤怠機能

画面は `Components/Pages/AttendanceClock.razor`（`attendance`。ルート `/` もこの画面で、ログイン後の既定の画面になる。Entity の `Attendance` と名前がぶつかるので画面のクラス名は `AttendanceClock`）、処理は `Logic/Attendance/AttendanceService.cs`、SQL は `Sql/Attendance/`、テーブル `attendances` の DDL は `Ddl/CreateAuthTables_<DB 種別>.sql`（ログイン機能のテーブルと同じファイル）。

- ログインユーザー・日本時間の日付ごとに 1 行（`UNIQUE (user_id, work_date)`）。出勤・退勤できるかはサーバー側で `AttendanceStatus`（`CanClockIn` / `CanClockOut`）を使って判定し、打刻の SQL は `Sql/Attendance/ClockIn_<DB 種別>.sql` / `ClockOut.sql`。日付はボタンを押した時点のサーバー時刻で判定する。
- `attendances` テーブルにはメモ用の `comment` 列があるが、今のプログラムでは使っていない。
- 日付をまたぐ勤務・打刻の訂正は扱わない仕様。
- 現在時刻の表示は `AttendanceClock.razor.js` がブラウザで行う（サーバー側のタイマーで毎秒再描画しない）。日本時間の日付が変わると `OnDateChanged` で打刻状況を読み直す。
- 勤怠一覧は `Components/Pages/AttendanceList.razor`（`attendance/list`）。ログインユーザーの当月分（日本時間）を月の全日 1 日 1 行で表示する（表示のみ）。土日の行は app.css の `attendance-row-saturday` / `attendance-row-sunday` で色分けし、祝日は扱わない。勤務時間は `AttendanceService` で計算し（休憩は `AttendanceService.BreakMinutes`、今は 60 分固定）、退勤していない日は空欄、合計にも含めない。
