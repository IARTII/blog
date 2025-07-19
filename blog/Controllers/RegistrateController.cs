using Microsoft.AspNetCore.Mvc;
using YourNamespace.Postgres;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegisterController : ControllerBase
    {
        private readonly Db _db;

        public RegisterController(Db db)
        {
            _db = db;
        }

        public class RegisterRequest
        {
            public string Username { get; set; }
            public string Password { get; set; } // SHA-256 хэш с клиента
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Имя и пароль обязательны." });

            if (await _db.UserExistsAsync(request.Username))
                return Conflict(new { message = "Пользователь уже существует." });

            await _db.CreateUserAsync(request.Username, request.Password);
            return Ok(new { message = "Пользователь зарегистрирован." });
        }
    }
}
