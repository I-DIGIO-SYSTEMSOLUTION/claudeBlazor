#nullable disable

using Microsoft.EntityFrameworkCore;

namespace LogicCommon
{
    public class DbContextWeatherForecast : DbContext
    {
        public DbSet<WeatherForecast> WeatherForecasts { get; set; }

        public DbContextWeatherForecast(DbContextOptions<DbContextWeatherForecast> options) : base(options) { }
    }
}
