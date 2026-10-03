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

- UI は MudBlazor（Bootstrap は読み込んでいない）。`MudThemeProvider` などのプロバイダーは `MainLayout.razor` に置いている。`MudNavLink` の `Href` も相対パス（先頭に `/` を付けない）にすること。
- パッケージの静的アセット（`_content/MudBlazor/...`）は、Development 環境か publish 後でないと配信されない（Production 環境で `bin/` の dll を直接起動すると 500 になる）。
- csproj で `BlazorDisableThrowNavigationException` を有効にしています。404 は `UseStatusCodePagesWithReExecute("/not-found")` と `Router` の `NotFoundPage` で処理します。
