#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace LogicCommon
{
    // PostgreSQL の weather_forecasts テーブル
    [Table("weather_forecasts")]
    public class WeatherForecast
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("date")]
        public DateOnly Date { get; set; }

        [Column("temperature_c")]
        public int TemperatureC { get; set; }

        [Column("summary")]
        public string Summary { get; set; }

        // 作成日・最終更新日 (timestamptz。Npgsql では Kind = Utc の DateTime になる)
        // 作成日は INSERT 時に列の既定値 now() で入る。最終更新日は Update.sql で更新し、他の人の変更の検出にも使う
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [NotMapped]
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
