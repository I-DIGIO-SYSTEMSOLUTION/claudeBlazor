namespace LogicCommon
{
    // 勤怠一覧画面に出す 1 か月分の勤怠 (月の全日を 1 日 1 行で持つ。打刻の無い日は ClockInAt が null)
    // プリレンダリングで読んだ値を対話モードに引き継ぐ ([PersistentState]) ので、JSON にできる形にしている
    public sealed class AttendanceMonth
    {
        public int Year { get; set; }
        public int Month { get; set; }

        // 読み込んだ時点の日本時間の日付 (退勤していない日が「勤務中」か「打刻漏れ」かの判定に使う)
        public DateOnly Today { get; set; }

        public List<AttendanceDay> Days { get; set; } = [];

        // 出勤日数 (出勤を打刻した日の数。退勤していない日も含む)
        public int WorkDays => Days.Count(d => d.ClockInAt != null);

        // 合計勤務時間 (分)。退勤を打刻した日だけを合計する
        public int TotalWorkMinutes => Days.Sum(d => d.WorkMinutes ?? 0);
    }

    public sealed class AttendanceDay
    {
        public DateOnly Date { get; set; }
        public DateTime? ClockInAt { get; set; }
        public DateTime? ClockOutAt { get; set; }
        public string? Comment { get; set; }

        // 勤務時間 (分)。休憩時間を差し引いた値で、退勤していない日は null
        public int? WorkMinutes { get; set; }
    }
}
