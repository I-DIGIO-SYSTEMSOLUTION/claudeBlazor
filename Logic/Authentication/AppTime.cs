namespace LogicCommon
{
    // 画面に出す日時 (DB の日時は UTC なので、日本時間に変換して表示する)
    public static class AppTime
    {
        private static readonly TimeZoneInfo DisplayTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        public static string Format(DateTime? utc, string format = "yyyy/MM/dd HH:mm")
        {
            if (utc is not DateTime value)
            {
                return "";
            }
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), DisplayTimeZone).ToString(format);
        }
    }
}
