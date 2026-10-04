# CLAUDE.md

このファイルは、このリポジトリで作業する Claude Code (claude.ai/code) 向けのガイドです。

## 概要

既定のテンプレートをもとにした Blazor Web App（.NET 10、Interactive Server レンダーモード）です。**SageMaker Studio Code Editor** 上で動かし、そのポートプロキシ（`https://<Studio のドメイン>/codeeditor/default/proxy/5000/`）経由でアクセスできるようにカスタマイズしています。テンプレート以外のコードの大半は、パスにプレフィックスが付くこのプロキシの配下で Blazor を動かすためのものです。コード内のコメントは日本語です。

## コマンド

```bash
dotnet build -c Debug MyBlazorApp.csproj
dotnet run --launch-profile http        # http://localhost:5000 で待ち受け、ProxyBasePath を設定
```

- dotnet は apt でインストールしてあり、本体は `/usr/bin/dotnet`、`DOTNET_ROOT=/usr/lib/dotnet` です。`.vscode/tasks.json` と `launch.json` はこれらのパスを直接書いています。
- VS Code のデバッグ（`.vscode/launch.json`）は `bin/Debug/net10.0/MyBlazorApp.dll` を直接起動します。そのため `Properties/launchSettings.json` は**適用されません**（待ち受けアドレスは launch.json 側で `ASPNETCORE_URLS=http://localhost:5000` を設定）。この起動方法では `ProxyBasePath` が未設定なので、`App.razor` 内のクライアント側 base href スクリプトによって動作しています。
- このリポジトリにはテストも linter もありません。

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
- 条件が可変な SQL は `SqlStatementStore.Build(name, new { ... })` を使う（`LogicCommon/SqlTemplate.cs`）。`--#if 名前` 〜 `--#endif` の行は引数が指定されたとき（null・空文字・空コレクション以外）だけ残り、`@名前` は位置パラメータに置き換わる（コレクションは IN 句用に要素数分へ展開）。条件を外しても成り立つよう `WHERE 1 = 1` にしておく。例: `Sql/WeatherForecast/Search.sql`。
- DB ごとに SQL が異なる場合は `Search_PostgreSQL.sql` / `Search_SQLServer.sql` のようにファイル名の末尾に `_<DB 種別>`（`databaseType` の値）を付ける。`Get` / `Build` には DB 種別なしの名前を渡し、DB 用のファイルがあればそれを、無ければ共通の `Search.sql` を使う。`_` は DB 種別の区切り専用で、`_` の後ろが DB 種別でないファイル（綴り間違いを含む）はエラーログ（log4net の ERROR ファイルにも出力）を出して読み込まない。
- ログは 2 系統ある。ASP.NET Core 標準の `ILogger`（コンソール出力）と、LogicCommon 配下が使う log4net（`log4net.config`、出力先は `<コンテンツルート>/logs/yyyyMMdd_app_<DEBUG|INFO|WARN|ERROR>.log` で日付・レベルごとに分かれる、Program.cs で `ConfigureAndWatch`）。EF Core が実行した SQL は `DatabaseConnectionHelper.UseDatabase` の `LogTo` で log4net に DEBUG で出し、Development のときだけパラメータの値も出す。
- csproj で `BlazorDisableThrowNavigationException` を有効にしています。404 は `UseStatusCodePagesWithReExecute("/not-found")` と `Router` の `NotFoundPage` で処理します。
