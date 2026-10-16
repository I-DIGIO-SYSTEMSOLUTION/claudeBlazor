#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace LogicCommon
{
    // attendances テーブル (ユーザー・日付ごとに 1 行)
    [Table("attendances")]
    public class Attendance
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("user_id")]
        public long UserId { get; set; }

        // 日本時間の日付
        [Column("work_date")]
        public DateOnly WorkDate { get; set; }

        [Column("clock_in_at")]
        public DateTime ClockInAt { get; set; }

        // NULL は未退勤
        [Column("clock_out_at")]
        public DateTime? ClockOutAt { get; set; }

        [Column("comment")]
        public string Comment { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
