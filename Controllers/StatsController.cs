using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Blogs.Controllers
{
    public class StatsController : Controller
    {
        private readonly IConfiguration _config;

        public StatsController(IConfiguration config)
        {
            _config = config;
        }

        public async Task<IActionResult> Stats()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStats([FromQuery] string? date, [FromQuery] string? user_id, [FromQuery] string? tag)
        {
            var cs = _config.GetConnectionString("DefaultConnection");
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();

            var sql = @"
                SELECT COUNT(*)
                FROM posts p
                LEFT JOIN post_tags pt ON p.id = pt.post_id
                LEFT JOIN tags t ON t.id = pt.tag_id
                WHERE 1=1";

            await using var cmd = new NpgsqlCommand { Connection = conn };

            if (!string.IsNullOrWhiteSpace(date))
            {
                if (DateTime.TryParse(date, out var dt))
                {
                    sql += " AND DATE(p.created_at) = @date";
                    cmd.Parameters.AddWithValue("date", dt.Date);
                }
                else
                {
                    return BadRequest(new { message = "Неверный формат даты. Используй YYYY-MM-DD." });
                }
            }

            if (!string.IsNullOrWhiteSpace(user_id))
            {
                if (int.TryParse(user_id, out var uid))
                {
                    sql += " AND p.user_id = @userId";
                    cmd.Parameters.AddWithValue("userId", uid);
                }
                else
                {
                    return BadRequest(new { message = "user_id должен быть числом." });
                }
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                sql += " AND t.name = @tag";
                cmd.Parameters.AddWithValue("tag", tag);
            }

            cmd.CommandText = sql;
            var result = await cmd.ExecuteScalarAsync();
            var count = Convert.ToInt32(result);

            return Ok(new { count });
        }
    }
}
