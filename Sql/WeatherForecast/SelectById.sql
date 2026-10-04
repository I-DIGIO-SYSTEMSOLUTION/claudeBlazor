-- id を指定して 1 行取得する。変更の保存が競合したときに、今の値を確認するために使う
SELECT id, date, temperature_c, summary, created_at, updated_at
FROM weather_forecasts
WHERE id = {0}
