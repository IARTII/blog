using Blogs.Domain.Services;
using Blogs.Models;
using Npgsql;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Blogs.Service
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<(bool Success, string Message)> Inlet(User request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return (false, "Имя пользователя и пароль обязательны");

            var username = request.Username;
            var password = request.Password;

            var connectionString = _config.GetConnectionString("DefaultConnection");
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var checkCmd = new NpgsqlCommand("SELECT password_hash FROM users WHERE username = @username", conn);
            checkCmd.Parameters.AddWithValue("username", username);
            var storedHash = (string?)await checkCmd.ExecuteScalarAsync();

            if (storedHash == null)
                return (false, "Пользователь не существует");

            if (!BCrypt.Net.BCrypt.Verify(password, storedHash))
                return (false, "Неверный пароль");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                Console.WriteLine("HttpContext = NULL");
                return (false, "HttpContext недоступен");
            }

            await httpContext.SignInAsync("CookieAuthBlog", principal);

            return (true, "Успешный вход");
        }

        public async Task<(bool Success, string Message)> Register(User request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return (false, "Имя пользователя и пароль обязательны");
            }

            var username = request.Username;
            var password = request.Password;

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var connectionString = _config.GetConnectionString("DefaultConnection");
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            var checkCmd = new NpgsqlCommand("SELECT 1 FROM users WHERE username = @username", conn);
            checkCmd.Parameters.AddWithValue("username", username);
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists != null)
            {
                return (false, "Пользователь уже существует.");
            }
                
            var insertCmd = new NpgsqlCommand("INSERT INTO users (username, password_hash) VALUES (@username, @password_hash)", conn);
            insertCmd.Parameters.AddWithValue("username", username);
            insertCmd.Parameters.AddWithValue("password_hash", passwordHash);
            await insertCmd.ExecuteNonQueryAsync();
            conn.Close();

            return (true, "Пользователь зарегистрирован");
        }

        public async Task<(bool Success, string Message)> Logout()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                await httpContext.SignOutAsync();
                return (true, "Успешный выход!");
            }
            return (false, "Ошибка при удалении Cookie");
        }
    }
}
