using MudBlazorDemo.Client.Models;

namespace MudBlazorDemo.Client.Services
{
    public interface IWeatherForecastService
    {
        Task<WeatherForecast[]?> Get();
    }
}