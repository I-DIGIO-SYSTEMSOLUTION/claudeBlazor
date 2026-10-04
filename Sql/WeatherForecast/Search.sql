-- 条件が可変な検索。SqlStatementStore.Build で組み立てる (書き方は LogicCommon/SqlTemplate.cs を参照)
--   --#if 名前 から --#endif までの行は、その引数が指定されているときだけ残る (null・空文字・空のリストは指定なし)
--   アットマーク付きの名前は引数の値のパラメータに置き換わり、リストは要素数分に展開される (IN 句用)
-- どの条件を外しても SQL として成り立つよう WHERE 1 = 1 にしておく
SELECT id, date, temperature_c, summary, created_at, updated_at
FROM weather_forecasts
WHERE 1 = 1
--#if summaries
  AND summary IN (@summaries)
--#endif
--#if minTemperatureC
  AND temperature_c >= @minTemperatureC
--#endif
ORDER BY date
