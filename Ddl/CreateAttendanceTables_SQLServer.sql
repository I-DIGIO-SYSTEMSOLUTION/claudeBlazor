-- 勤怠管理のテーブル (SQL Server)。PostgreSQL 版は CreateAttendanceTables_PostgreSQL.sql
-- 外部キーは設定しない (テーブル間の整合性はアプリ側で守る)
-- 日時は datetime2 に UTC で保存する (アプリの Entity が DateTime のため)。スキーマは dbo を前提にしている

-- ============================================================
-- 勤怠 (ユーザー・日付ごとに 1 行。出勤ボタンで作り、退勤ボタンで退勤時刻を入れる)
-- ============================================================
CREATE TABLE attendances (
    id            bigint          IDENTITY(1, 1) NOT NULL,
    user_id       bigint          NOT NULL,
    work_date     date            NOT NULL,
    clock_in_at   datetime2       NOT NULL,
    clock_out_at  datetime2       NULL,
    comment       nvarchar(1024)  NULL,
    created_at    datetime2       NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at    datetime2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_attendances PRIMARY KEY (id),
    -- 出勤は 1 日 1 回 (同時に押されても 2 行目は作られない)
    CONSTRAINT uq_attendances_user_id_work_date UNIQUE (user_id, work_date)
);

EXEC sys.sp_addextendedproperty N'MS_Description', N'勤怠 (ユーザー・日付ごとに 1 行)', N'SCHEMA', N'dbo', N'TABLE', N'attendances';
EXEC sys.sp_addextendedproperty N'MS_Description', N'勤怠ID', N'SCHEMA', N'dbo', N'TABLE', N'attendances', N'COLUMN', N'id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ユーザーID (users.id)', N'SCHEMA', N'dbo', N'TABLE', N'attendances', N'COLUMN', N'user_id';
EXEC sys.sp_addextendedproperty N'MS_Description', N'勤務日 (日本時間の日付)', N'SCHEMA', N'dbo', N'TABLE', N'attendances', N'COLUMN', N'work_date';
EXEC sys.sp_addextendedproperty N'MS_Description', N'出勤日時', N'SCHEMA', N'dbo', N'TABLE', N'attendances', N'COLUMN', N'clock_in_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'退勤日時 (NULL は未退勤)', N'SCHEMA', N'dbo', N'TABLE', N'attendances', N'COLUMN', N'clock_out_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'コメント (出勤時に保存し、退勤時の内容で上書きする)', N'SCHEMA', N'dbo', N'TABLE', N'attendances', N'COLUMN', N'comment';
EXEC sys.sp_addextendedproperty N'MS_Description', N'作成日時', N'SCHEMA', N'dbo', N'TABLE', N'attendances', N'COLUMN', N'created_at';
EXEC sys.sp_addextendedproperty N'MS_Description', N'更新日時', N'SCHEMA', N'dbo', N'TABLE', N'attendances', N'COLUMN', N'updated_at';
