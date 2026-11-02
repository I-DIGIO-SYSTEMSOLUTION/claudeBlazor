using LogicCommon;

namespace MyBlazorApp.Tests
{
    // 出勤・退勤できるかの判定 (AttendanceStatus.CanClockIn / CanClockOut) のテスト
    // AttendanceService.ClockInAsync / ClockOutAsync は、この判定が OK のときだけ SQL を実行する
    public class AttendanceStatusTests
    {
        private static readonly DateOnly WorkDate = new(2026, 5, 1);
        private static readonly DateTime ClockInAt = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);   // 日本時間 9:00
        private static readonly DateTime ClockOutAt = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);  // 日本時間 18:00

        // 未出勤 (その日の行が無い)
        private static AttendanceStatus NotClockedIn() => new() { WorkDate = WorkDate };

        // 出勤済み・未退勤
        private static AttendanceStatus Working() => new() { WorkDate = WorkDate, ClockInAt = ClockInAt };

        // 退勤済み
        private static AttendanceStatus ClockedOut() => new() { WorkDate = WorkDate, ClockInAt = ClockInAt, ClockOutAt = ClockOutAt };

        [Fact]
        public void 未出勤なら出勤できる()
        {
            Assert.True(NotClockedIn().CanClockIn);
        }

        [Fact]
        public void 未出勤なら退勤できない()
        {
            Assert.False(NotClockedIn().CanClockOut);
        }

        [Fact]
        public void 出勤済みで未退勤なら退勤できる()
        {
            Assert.True(Working().CanClockOut);
        }

        [Fact]
        public void 退勤済みなら退勤できない()
        {
            Assert.False(ClockedOut().CanClockOut);
        }
    }
}
