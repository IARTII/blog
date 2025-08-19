using Blogs.Domain.Services;
using Blogs.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Blogs.Controllers
{
    public class StatsController : Controller
    {
        private readonly IStatsService _statsService;
        private readonly ILogger<StatsController> _logger;

        public StatsController(IStatsService statsService, ILogger<StatsController> logger)
        {
            _statsService = statsService;
            _logger = logger;
        }

        public async Task<IActionResult> Stats()
        {
            _logger.LogInformation("Пользователь {User} открыл страницу статистики", HttpContext.User.Identity?.Name);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStats([FromQuery] string? date, [FromQuery] string? user_id, [FromQuery] string? tag)
        {
            _logger.LogInformation(
                "Запрос статистики. Дата: {Date}, UserId: {UserId}, Тег: {Tag}",
                date, user_id, tag);

            var (success, message, count) = await _statsService.GetStats(date, user_id, tag);

            if (!success)
            {
                _logger.LogWarning(
                    "Ошибка при получении статистики. Дата: {Date}, UserId: {UserId}, Тег: {Tag}, Ошибка: {Message}",
                    date, user_id, tag, message);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message });
                }

                throw new CustomException(message, 500);
            }

            _logger.LogInformation(
                "Статистика успешно получена. Количество: {Count}, Дата: {Date}, UserId: {UserId}, Тег: {Tag}",
                 count, date, user_id, tag);

            return Ok(new { success = true, count });
        }
    }
}
