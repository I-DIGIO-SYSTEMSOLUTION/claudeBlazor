-- 一覧で編集した行を、1 回の UPDATE でまとめて更新する (BULK UPDATE)。SqlStatementStore.Build で組み立てる
-- rows には 1 行ごとに (id, 日付, 気温, 概況, 読み込んだ時点の最終更新日) の配列を渡し、VALUES の行に展開される
-- 呼び出し側の Weather.razor で 400 行ずつに分けて実行する (SQL Server のパラメータ数の上限に合わせているため)
-- 他の人の変更を上書きしないよう、読み込んだ時点の最終更新日と今の最終更新日が同じ行だけを更新し、最終更新日を今の日時にする。
-- RETURNING で更新できた行の id を返す。返ってこなかった id は、読み込んだ後に他の人が変更または削除している
-- now() はトランザクションの開始時刻なので、まとめて保存した行は同じ最終更新日になる
UPDATE weather_forecasts AS t
SET date = v.date,
    temperature_c = v.temperature_c,
    summary = v.summary,
    updated_at = now()
FROM (VALUES @rows) AS v (id, date, temperature_c, summary, updated_at)
WHERE t.id = v.id
  AND t.updated_at = v.updated_at
RETURNING t.id AS "Value"
