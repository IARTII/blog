using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Scripting;
using Npgsql;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Blogs.Models;

namespace Blogs.Controllers
{
    public class AuthController : Controller
    {
        private readonly IConfiguration _config;

        public AuthController(IConfiguration config)
        {
            _config = config;
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
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Имя пользователя и пароль обязательны" });

            var username = request.Username;
            var password = request.Password;

            var connectionString = _config.GetConnectionString("DefaultConnection");
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var checkCmd = new NpgsqlCommand("SELECT username FROM users WHERE username = @username", conn);
            checkCmd.Parameters.AddWithValue("username", username);
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists == null)
                return Conflict(new { message = "Пользователь не существует." });

            var findCmd = new NpgsqlCommand("SELECT password_hash FROM users WHERE username = @username", conn);
            findCmd.Parameters.AddWithValue("username", username);
            var storedHash = (string)await findCmd.ExecuteScalarAsync();

            if (BCrypt.Net.BCrypt.Verify(password, storedHash))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                };
                var identity = new ClaimsIdentity(claims, "CookieAuthBlog");
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync("CookieAuthBlog", principal);

                return RedirectToAction("Posts", "Post");
            }

            return NotFound(new { message = "Неверный пароль" });
        }


        [HttpPost("register")]
        public ActionResult Register(User request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Имя пользователя и пароль обязательны" });
            }

            var username = request.Username;
            var password = request.Password;

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var connectionString = _config.GetConnectionString("DefaultConnection");
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open(); 

            // Проверка на существование пользователя
            var checkCmd = new NpgsqlCommand("SELECT 1 FROM users WHERE username = @username", conn);
            checkCmd.Parameters.AddWithValue("username", username);
            var exists = checkCmd.ExecuteScalarAsync();
            if (exists != null)
                return Conflict(new { message = "Пользователь уже существует." });

            // Создание пользователя
            var insertCmd = new NpgsqlCommand(
                "INSERT INTO users (username, password_hash) VALUES (@username, @password_hash)", conn);
            insertCmd.Parameters.AddWithValue("username", username);
            insertCmd.Parameters.AddWithValue("password_hash", passwordHash);
            insertCmd.ExecuteNonQueryAsync();
            conn.Close();

            return Ok(new { message = "Пользователь зарегистрирован" });
        }
    }
}
