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

        [NotMapped]
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
