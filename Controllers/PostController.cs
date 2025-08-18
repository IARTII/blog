using Blogs.Domain.Services;
using Blogs.Models;
using Blogs.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

namespace Blogs.Controllers
{
    public class PostController : Controller
    {
        private readonly IPostService _postService;

        public PostController(IPostService postService)
        {
            _postService = postService;
        }

        public async Task<IActionResult> Posts()
        {
            var (success, message, model) = await _postService.Posts();

            if (!success)
                return BadRequest(new { message });

            return View(model);
        }

        public ActionResult AddPost(int id)
        {
            return View();
        }

        [HttpPost("newPost")]
        public async Task<IActionResult> NewPost(string title, string content, string tags, IFormFile? image)
        {
            var (success, message) = await _postService.NewPost(title, content, tags, image);

            if (!success)
                return BadRequest(new { message });

            return RedirectToAction("Posts", "Post");
        }

        [HttpPost("like_post")]
        public async Task<IActionResult> LikePost([FromBody] LikePostRequest request)
        {
            var (success, message, liked) = await _postService.LikePost(request);

            if (!success)
                return BadRequest(new { message });

            return Json(liked);
        }
    }
}
