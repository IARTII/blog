using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blogs.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("Error/{statusCode?}")]
        public IActionResult Error(int? statusCode = 500)
        {
            var message = statusCode switch
            {
                404 => "Страница не найдена.",
                403 => "Доступ запрещён.",
                _ => "Произошла непредвиденная ошибка."
            };

            switch (statusCode)
            {
                case 404:
                    _logger.LogWarning("Ошибка 404: страница не найдена. URL: {Path}", HttpContext.Request.Path);
                    break;
                case 403:
                    _logger.LogWarning("Ошибка 403: доступ запрещён. URL: {Path}", HttpContext.Request.Path);
                    break;
                default:
                    _logger.LogError("Ошибка {StatusCode}: {Message}. URL: {Path}", statusCode, message, HttpContext.Request.Path);
                    break;
            }

            ViewData["StatusCode"] = statusCode ?? 500;
            ViewData["Message"] = message;

            return View("Error");
        }

        [Route("Error/Handle")]
        public IActionResult Handle(string? message)
        {
            _logger.LogError("CustomException: {Message}. URL: {Path}", message, HttpContext.Request.Path);

            ViewData["StatusCode"] = 500;
            ViewData["Message"] = message ?? "Произошла ошибка.";

            return View("Error");
        }
    }
}
