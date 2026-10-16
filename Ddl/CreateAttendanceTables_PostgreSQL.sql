-- 勤怠管理のテーブル (PostgreSQL)。SQL Server 版は CreateAttendanceTables_SQLServer.sql
-- 外部キーは設定しない (テーブル間の整合性はアプリ側で守る)

-- ============================================================
-- 勤怠 (ユーザー・日付ごとに 1 行。出勤ボタンで作り、退勤ボタンで退勤時刻を入れる)
-- ============================================================
CREATE TABLE attendances (
    id            bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id       bigint        NOT NULL,
    work_date     date          NOT NULL,
    clock_in_at   timestamptz   NOT NULL,
    clock_out_at  timestamptz   NULL,
    comment       varchar(1024) NULL,
    created_at    timestamptz   NOT NULL DEFAULT now(),
    updated_at    timestamptz   NOT NULL DEFAULT now(),
    -- 出勤は 1 日 1 回 (同時に押されても 2 行目は作られない)
    CONSTRAINT uq_attendances_user_id_work_date UNIQUE (user_id, work_date)
);

COMMENT ON TABLE  attendances              IS '勤怠 (ユーザー・日付ごとに 1 行)';
COMMENT ON COLUMN attendances.id           IS '勤怠ID';
COMMENT ON COLUMN attendances.user_id      IS 'ユーザーID (users.id)';
COMMENT ON COLUMN attendances.work_date    IS '勤務日 (日本時間の日付)';
COMMENT ON COLUMN attendances.clock_in_at  IS '出勤日時';
COMMENT ON COLUMN attendances.clock_out_at IS '退勤日時 (NULL は未退勤)';
COMMENT ON COLUMN attendances.comment      IS 'コメント (出勤時に保存し、退勤時の内容で上書きする)';
COMMENT ON COLUMN attendances.created_at   IS '作成日時';
COMMENT ON COLUMN attendances.updated_at   IS '更新日時';
