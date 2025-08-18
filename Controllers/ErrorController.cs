using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Blogs.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode?}")]
        public IActionResult Error(int? statusCode = 500)
        {
            var message = statusCode switch
            {
                404 => "Страница не найдена.",
                403 => "Доступ запрещён.",
                _ => "Произошла непредвиденная ошибка."
            };

            ViewData["StatusCode"] = statusCode ?? 500;
            ViewData["Message"] = message;

            return View();
        }
    }
}
