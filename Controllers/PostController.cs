using Blogs.Domain.Services;
using Blogs.Models;
using Blogs.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Blogs.Controllers
{
    public class PostController : Controller
    {
        private readonly IPostService _postService;
        private readonly ILogger<PostController> _logger;

        public PostController(IPostService postService, ILogger<PostController> logger)
        {
            _postService = postService;
            _logger = logger;
        }

        public async Task<IActionResult> Posts()
        {
            _logger.LogInformation("Запрос списка постов пользователем {User}", HttpContext.User.Identity?.Name);

            var (success, message, model) = await _postService.Posts();

            if (!success)
            {
                _logger.LogWarning("Ошибка получения постов: {Message}", message);
                return BadRequest(new { message });
            }

            _logger.LogInformation("Список постов успешно загружен, количество: {Count}", model?.Count());
            return View(model);
        }

        public ActionResult AddPost(int id)
        {
            _logger.LogDebug("Открыта форма добавления поста (id={Id}) пользователем {User}",
                id, HttpContext.User.Identity?.Name);

            return View();
        }

        [HttpPost("newPost")]
        public async Task<IActionResult> NewPost(string title, string content, string tags, IFormFile? image)
        {
            _logger.LogInformation("Попытка создать новый пост пользователем {User}. Заголовок: {Title}, Теги: {Tags}",
                HttpContext.User.Identity?.Name, title, tags);

            var (success, message) = await _postService.NewPost(title, content, tags, image);

            if (!success)
            {
                _logger.LogWarning("Ошибка при создании поста: {Message}", message);
                return BadRequest(new { message });
            }

            _logger.LogInformation("Пост '{Title}' успешно создан пользователем {User}", title, HttpContext.User.Identity?.Name);
            return RedirectToAction("Posts", "Post");
        }

        [HttpPost("like_post")]
        public async Task<IActionResult> LikePost([FromBody] LikePostRequest request)
        {
            _logger.LogInformation("Пользователь {User} пытается лайкнуть пост {PostId}",
                HttpContext.User.Identity?.Name, request?.PostId);

            var (success, message, liked) = await _postService.LikePost(request);

            if (!success)
            {
                _logger.LogWarning("Ошибка лайка поста {PostId}: {Message}", request?.PostId, message);
                return BadRequest(new { message });
            }

            _logger.LogInformation("Пользователь {User} {Action} пост {PostId}",
                HttpContext.User.Identity?.Name,
                Convert.ToBoolean(liked) ? "лайкнул" : "снял лайк с",
                request?.PostId);

            return Json(liked);
        }
    }
}
