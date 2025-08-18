using Blogs.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Blogs.Controllers
{
    public class StatsController : Controller
    {
        private readonly IStatsService _statsService;

        public StatsController(IStatsService statsService)
        {
            _statsService = statsService;
        }

        public async Task<IActionResult> Stats()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStats([FromQuery] string? date, [FromQuery] string? user_id, [FromQuery] string? tag)
        {
            var (success, message, count) = await _statsService.GetStats(date, user_id, tag);

            if (!success)
                return BadRequest(new { message });

            Console.WriteLine(count.ToString());
            return Ok(new {count});
        }
    }
}
