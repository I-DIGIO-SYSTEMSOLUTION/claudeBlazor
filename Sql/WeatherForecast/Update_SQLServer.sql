-- 一覧で編集した行を、1 回の UPDATE でまとめて更新する (BULK UPDATE、SQL Server 用)。SqlStatementStore.Build で組み立てる
-- rows には 1 行ごとに (id, 日付, 気温, 概況, 読み込んだ時点の最終更新日) の配列を渡し、VALUES の行に展開される
-- 他の人の変更を上書きしないよう、読み込んだ時点の最終更新日と今の最終更新日が同じ行だけを更新し、最終更新日を今の日時 (UTC) にする。
-- OUTPUT で更新できた行の id を返す。返ってこなかった id は、読み込んだ後に他の人が変更または削除している
-- SYSUTCDATETIME() は 1 つの文の中では同じ値なので、まとめて保存した行は同じ最終更新日になる
-- 前提:
--   updated_at は datetime2(7) にすること (datetime だと精度が 3.33 ミリ秒で、画面の値と一致せず常に競合扱いになる)
--   テーブルにトリガーがある場合、INTO なしの OUTPUT は使えないため、OUTPUT INTO で一時テーブルに受ける形に変える
--   パラメータは 1 文で 2100 個までなので、一度に更新できるのは 419 行まで (呼び出し側の Weather.razor で 400 行ずつに分けている)
UPDATE t
SET t.[date] = v.[date],
    t.temperature_c = v.temperature_c,
    t.summary = v.summary,
    t.updated_at = SYSUTCDATETIME()
OUTPUT inserted.id AS [Value]
FROM weather_forecasts AS t
INNER JOIN (VALUES @rows) AS v (id, [date], temperature_c, summary, updated_at)
    ON t.id = v.id
   AND t.updated_at = v.updated_at
