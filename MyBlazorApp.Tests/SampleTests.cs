using LogicCommon;

namespace MyBlazorApp.Tests
{
    // データベースを使わない見本のテスト。テストの書き方の参考にする
    public class SampleTests
    {
        // UTC の 15:00 は日本時間 (UTC+9) では翌日の 0:00 になる
        [Fact]
        public void ToDisplayDate_UTCの15時は日本時間の翌日になる()
        {
            DateTime utc = new(2026, 4, 30, 15, 0, 0, DateTimeKind.Utc);

            DateOnly date = AppTime.ToDisplayDate(utc);

            Assert.Equal(new DateOnly(2026, 5, 1), date);
        }

        // 日時が無い (null) ときは空文字を返す
        [Fact]
        public void Format_nullなら空文字を返す()
        {
            Assert.Equal("", AppTime.Format(null));
        }

        // 出勤日数は出勤した日の数、合計勤務時間は勤務時間がある日だけを足す
        [Fact]
        public void AttendanceMonth_出勤日数と合計勤務時間を集計する()
        {
            DateTime clockIn = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            AttendanceMonth month = new()
            {
                Days =
                [
                    new AttendanceDay { Date = new DateOnly(2026, 5, 1), ClockInAt = clockIn, ClockOutAt = clockIn.AddHours(9), WorkMinutes = 480 },
                    new AttendanceDay { Date = new DateOnly(2026, 5, 2), ClockInAt = clockIn.AddDays(1), WorkMinutes = null },
                    new AttendanceDay { Date = new DateOnly(2026, 5, 3) },
                ],
            };

            Assert.Equal(2, month.WorkDays);
            Assert.Equal(480, month.TotalWorkMinutes);
        }
    }
}
