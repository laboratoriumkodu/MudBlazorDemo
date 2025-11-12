using Microsoft.AspNetCore.Mvc;
using MudBlazorDemo.Client.Models;

namespace MudBlazorDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SampleController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { message = "Hello from API" });
        }

        [HttpPost]
        public IActionResult Post([FromBody] dynamic data)
        {
            return Ok(new { received = data });
        }

        [HttpGet("weather")]
        public IActionResult GetWeatherForecast()
        {
            var startDate = DateOnly.FromDateTime(DateTime.Now);
            var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
            var forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = startDate.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = summaries[Random.Shared.Next(summaries.Length)]
            }).ToArray();

            return Ok(forecasts);
        }
    }
}
