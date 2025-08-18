using Blogs.Domain.Services;
using Blogs.Models;
using Blogs.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Blogs.Controllers
{
    public class CommentsController : Controller
    {
        private readonly ICommentsService _commentsService;

        public CommentsController(ICommentsService commentsService)
        {
            _commentsService = commentsService;
        }

        public async Task<IActionResult> Comments(int? postId)
        {
            var (success, message, model) = await _commentsService.Comments(postId);

            if (!success)
                return BadRequest(new { message });

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] Comment _model)
        {
            var (success, message, model) = await _commentsService.AddComment(_model);

            if (!success)
                return BadRequest(new { message });

            return Json(model);
        }
    }
}
