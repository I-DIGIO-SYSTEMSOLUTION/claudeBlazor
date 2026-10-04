-- パラメータは 0 始まりの番号を波かっこで囲んで指定する (EF Core が DB に合ったパラメータに変換する)
-- 注意: SQL 文全体が string.Format で処理されるため、コメント内でも波かっこを書かないこと (書く場合は二重にする)
DELETE FROM weather_forecasts WHERE id = {0}
