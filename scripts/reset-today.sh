#!/usr/bin/env bash
# 開発用: 日本時間の今日の打刻 (attendances の行) を全ユーザー分消す
set -euo pipefail

# リポジトリ直下に移動してから実行する (tools/ResetToday は Setting/ を相対パスで読む)
cd "$(dirname "$0")/.."
dotnet run --project tools/ResetToday
