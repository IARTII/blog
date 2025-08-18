using Blogs.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Blogs.Service
{
    public class StatsService : IStatsService
    {
        private readonly IConfiguration _config;

        public StatsService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<(bool Success, string Message, int count)> GetStats([FromQuery] string? date, [FromQuery] string? user_id, [FromQuery] string? tag)
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
                    return (false, "Неверный формат даты. Используйте YYYY-MM-DD.", 0);
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
                    return (false, "user_id должен быть числом.", 0);
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

            return (true, "Ok", count);
        }
    }
}
