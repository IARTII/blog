using Blogs.Domain.Services;
using Blogs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blogs.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        public ActionResult Index()
        {
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("Пользователь {User} уже авторизован, перенаправление на посты",
                    HttpContext.User.Identity?.Name);

                return RedirectToAction("Posts", "Post");
            }

            _logger.LogInformation("Открыта страница входа");
            return View();
        }

        public ActionResult Login(int id)
        {
            _logger.LogDebug("Перенапровление на страницу входа");
            return View();
        }

        [HttpPost("inlet")]
        public async Task<ActionResult> Inlet(User request)
        {
            _logger.LogInformation("Попытка входа пользователя {Username}", request?.Username);

            var (success, message) = await _authService.Inlet(request);

            if (!success)
            {
                _logger.LogWarning("Неудачная попытка входа: {Message}", message);
                return BadRequest(new { message });
            }

            _logger.LogInformation("Пользователь {Username} успешно вошёл", request?.Username);
            return RedirectToAction("Posts", "Post");
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register(User request)
        {
            _logger.LogInformation("Регистрация нового пользователя {Username}", request?.Username);

            var (success, message) = await _authService.Register(request);

            if (!success)
            {
                _logger.LogWarning("Ошибка регистрации: {Message}", message);
                return BadRequest(new { message });
            }

            _logger.LogInformation("Пользователь {Username} успешно зарегистрирован", request?.Username);
            return RedirectToAction("Posts", "Post");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("Попытка выхода пользователя {User}", HttpContext.User.Identity?.Name);

            var (success, message) = await _authService.Logout();

            if (!success)
            {
                _logger.LogError("Ошибка при выходе: {Message}", message);
                return BadRequest(new { message });
            }

            _logger.LogInformation("Пользователь {User} вышел из системы", HttpContext.User.Identity?.Name);
            return RedirectToAction("Index", "Auth");
        }
    }
}
