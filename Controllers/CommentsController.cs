using Blogs.Domain.Services;
using Blogs.Models;
using Blogs.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blogs.Controllers
{
    public class CommentsController : Controller
    {
        private readonly ICommentsService _commentsService;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(ICommentsService commentsService, ILogger<CommentsController> logger)
        {
            _commentsService = commentsService;
            _logger = logger;
        }

        public async Task<IActionResult> Comments(int? postId)
        {
            _logger.LogInformation("Запрос комментариев к посту {PostId}", postId);

            var (success, message, model) = await _commentsService.Comments(postId);

            if (!success)
            {
                _logger.LogWarning("Ошибка получения комментариев для поста {PostId}: {Message}", postId, message);
                return BadRequest(new { message });
            }

            _logger.LogInformation("Комментарии к посту {PostId} успешно загружены", postId);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] Comment comment)
        {
            _logger.LogInformation("Попытка добавить комментарий пользователем {User} к посту {PostId}: {Content}",
                HttpContext.User.Identity?.Name,
                comment?.postId,
                comment?.text);

            var (success, message, model) = await _commentsService.AddComment(comment);

            if (!success)
            {
                _logger.LogWarning("Ошибка при добавлении комментария к посту {PostId}: {Message}", comment?.postId, message);
                return BadRequest(new { message });
            }

            _logger.LogInformation("Комментарий успешно добавлен к посту {PostId}", comment?.postId);
            return Json(model);
        }
    }
}
