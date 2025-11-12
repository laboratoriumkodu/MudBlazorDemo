using MudBlazorDemo.Client.Models;
using System.Net.Http.Json;

namespace MudBlazorDemo.Client.Services
{
    public class WeatherForecastService : IWeatherForecastService
    {
        private readonly HttpClient http;

        public WeatherForecastService(HttpClient http)
        {
            this.http = http;
        }

        public async Task<WeatherForecast[]?> Get()
        {
            return await http.GetFromJsonAsync<WeatherForecast[]?>("api/sample/weather");
        }
    }
}
