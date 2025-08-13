using Blogs.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

namespace Blogs.Controllers
{
    public class PostController : Controller
    {
        private readonly IConfiguration _config;

        public PostController(IConfiguration config)
        {
            _config = config;
        }

        public async Task<IActionResult> Posts()
        {
            var posts = new List<Post>();

            await using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT 
                    p.id,
                    p.user_id,
                    p.title,
                    p.content,
                    p.created_at,
                    p.image_source AS image_url,
                    COALESCE(ARRAY_AGG(t.name) FILTER (WHERE t.name IS NOT NULL), '{}') AS tags
                FROM posts p
                LEFT JOIN post_tags pt ON pt.post_id = p.id
                LEFT JOIN tags t ON t.id = pt.tag_id
                GROUP BY p.id
                ORDER BY p.created_at DESC;
            ", conn);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                posts.Add(new Post
                {
                    id = reader["id"].ToString(),
                    user_id = reader["user_id"].ToString(),
                    title = reader["title"].ToString(),
                    contend = reader["content"].ToString(),
                    created_at = (DateTime)reader["created_at"],
                    image_url = reader.IsDBNull(reader.GetOrdinal("image_url")) ? null : reader["image_url"].ToString(),
                    Tags = reader.IsDBNull(reader.GetOrdinal("tags"))
                        ? new List<string>()
                        : reader.GetFieldValue<string[]>(reader.GetOrdinal("tags")).ToList()
                });
            }

            return View(posts);
        }


        public ActionResult AddPost(int id)
        {
            return View();
        }

        [HttpPost("newPost")]
        public async Task<IActionResult> NewPost(string title, string content, string tags, IFormFile? image)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest(new { message = "Пользователь не найден" });
            }

            var connectionString = _config.GetConnectionString("DefaultConnection");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                // Получаем user_id
                var getUserCmd = new NpgsqlCommand(
                    "SELECT id FROM users WHERE username = @username",
                    connection, transaction);
                getUserCmd.Parameters.AddWithValue("@username", username);
                var userIdObj = await getUserCmd.ExecuteScalarAsync();
                if (userIdObj == null)
                    return BadRequest(new { message = "Пользователь не найден" });

                long userId = Convert.ToInt64(userIdObj);

                // Обрабатываем изображение
                string? imagePath = null;
                if (image != null && image.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
                    var savePath = Path.Combine("wwwroot", "images", fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                    await using var stream = System.IO.File.Create(savePath);
                    await image.CopyToAsync(stream);
                    imagePath = "/images/" + fileName;
                }

                // Вставляем пост
                var insertPostCmd = new NpgsqlCommand(
                    "INSERT INTO posts (user_id, title, content, created_at, image_source) " +
                    "VALUES (@user_id, @title, @content, NOW(), @image) RETURNING id",
                    connection, transaction);
                insertPostCmd.Parameters.AddWithValue("@user_id", userId);
                insertPostCmd.Parameters.AddWithValue("@title", title);
                insertPostCmd.Parameters.AddWithValue("@content", content);
                insertPostCmd.Parameters.AddWithValue("@image", (object?)imagePath ?? DBNull.Value);

                var postIdObj = await insertPostCmd.ExecuteScalarAsync();
                long postId = Convert.ToInt64(postIdObj);

                // Обрабатываем теги
                var tagList = tags?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => t.ToLowerInvariant())
                    .Distinct()
                    ?? Enumerable.Empty<string>();

                foreach (var tagName in tagList)
                {
                    long tagId;

                    // Ищем тег
                    var findTagCmd = new NpgsqlCommand("SELECT id FROM tags WHERE name = @name", connection, transaction);
                    findTagCmd.Parameters.AddWithValue("@name", tagName);
                    var tagIdObj = await findTagCmd.ExecuteScalarAsync();

                    if (tagIdObj != null)
                    {
                        tagId = Convert.ToInt64(tagIdObj);
                    }
                    else
                    {
                        var insertTagCmd = new NpgsqlCommand(
                            "INSERT INTO tags (name) VALUES (@name) RETURNING id",
                            connection, transaction);
                        insertTagCmd.Parameters.AddWithValue("@name", tagName);
                        tagId = Convert.ToInt64(await insertTagCmd.ExecuteScalarAsync());
                    }

                    var insertPostTagCmd = new NpgsqlCommand(
                        "INSERT INTO post_tags (post_id, tag_id) VALUES (@postId, @tagId)",
                        connection, transaction);
                    insertPostTagCmd.Parameters.AddWithValue("@postId", postId);
                    insertPostTagCmd.Parameters.AddWithValue("@tagId", tagId);
                    await insertPostTagCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                TempData["Success"] = "Пост добавлен";
                return RedirectToAction("Posts", "Post");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine("Ошибка при добавлении поста: " + ex.Message);
                return StatusCode(500, "Ошибка сервера при добавлении поста.");
            }
        }

        [HttpGet("posts-with-tags")]
        public async Task<IActionResult> GetPostsWithTags()
        {
            var posts = new List<object>();

            await using var connection = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT 
    p.id,
    p.title,
    p.contend,
    p.image_source AS image_url,
    p.created_at,
    u.username,
    COALESCE(ARRAY_AGG(t.name) FILTER (WHERE t.name IS NOT NULL), '{}') AS tags
FROM posts p
JOIN users u ON u.id = p.user_id
LEFT JOIN post_tags pt ON pt.post_id = p.id
LEFT JOIN tags t ON t.id = pt.tag_id
GROUP BY p.id, u.username
ORDER BY p.created_at DESC;

            ", connection);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                posts.Add(new
                {
                    id = reader.GetInt32(0),
                    title = reader.GetString(1),
                    contend = reader.GetString(2),
                    image_url = reader.IsDBNull(3) ? null : reader.GetString(3),
                    created_at = reader.GetDateTime(4),
                    username = reader.GetString(5),
                    tags = reader.IsDBNull(6) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(6)
                });
            }

            return Ok(posts);
        }

    }
}
