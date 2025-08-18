using Blogs.Domain.Services;
using Blogs.Models;
using Microsoft.AspNetCore.Mvc;


namespace Blogs.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        public ActionResult Index()
        {
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Posts", "Post");
            }

            return View();
        }

        public ActionResult login(int id)
        {
            return View();
        }

        [HttpPost("inlet")]
        public async Task<ActionResult> inlet(User request)
        {
            var (success, message) = await _authService.Inlet(request);

            if (!success)
                return BadRequest(new { message });

            return RedirectToAction("Posts", "Post");
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register(User request)
        {
            var (success, message) = await _authService.Register(request);

            if (!success)
                return BadRequest(new { message });

            return RedirectToAction("Posts", "Post");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var (success, message) = await _authService.Logout();

            if (!success)
                return BadRequest(new { message });

            return RedirectToAction("Index", "Auth");
        }
    }
}
