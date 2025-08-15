using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Blogs.Models;

namespace Blogs.Controllers
{
    public class CommentsController : Controller
    {
        private readonly IConfiguration _config;

        public CommentsController(IConfiguration config)
        {
            _config = config;
        }

        public async Task<IActionResult> Comments(int? postId)
        {
            if (postId == null) return BadRequest("postId не передан");

            var post = await GetPostAsync(postId.Value);
            if (post == null) return NotFound("Пост не найден");

            var comments = await GetCommentsAsync(postId.Value);

            var model = new PostWithComments
            {
                Post = post,
                Comments = comments,
                CurrentUsername = User.Identity?.Name ?? "Гость"
            };

            return View(model);
        }


        private async Task<Post> GetPostAsync(int postId)
        {
            var connStr = _config.GetConnectionString("DefaultConnection");
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("SELECT id, title, content, image_source, created_at FROM posts WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", postId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Post
                {
                    id = reader.GetInt32(0).ToString(),
                    title = reader.GetString(1),
                    contend = reader.GetString(2),
                    image_url = reader.IsDBNull(3) ? null : reader.GetString(3),
                    created_at = reader.GetDateTime(4)
                };
            }
            return null;
        }

        private async Task<List<Comment>> GetCommentsAsync(int postId)
        {
            var connStr = _config.GetConnectionString("DefaultConnection");
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT users.username, comments.content, comments.created_at
                FROM comments
                JOIN users ON users.id = comments.user_id
                WHERE comments.post_id = @postId
                ORDER BY comments.created_at ASC", conn);

            cmd.Parameters.AddWithValue("postId", postId);

            var list = new List<Comment>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Comment
                {
                    username = reader.GetString(0),
                    text = reader.GetString(1),
                    created_at = reader.GetDateTime(2)
                });
            }

            return list;
        }

        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] Comment model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var connStr = _config.GetConnectionString("DefaultConnection");
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            var cmdUser = new NpgsqlCommand("SELECT id FROM users WHERE username=@username", conn);
            cmdUser.Parameters.AddWithValue("username", model.username);
            int userId;
            await using (var reader = await cmdUser.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    return BadRequest(new { message = "Пользователь не найден" });
                userId = reader.GetInt32(0);
            }

            var cmdInsert = new NpgsqlCommand(@"
            INSERT INTO comments (post_id, user_id, content, created_at)
            VALUES (@postId, @userId, @content, @createdAt)", conn);

            cmdInsert.Parameters.AddWithValue("postId", model.postId);
            cmdInsert.Parameters.AddWithValue("userId", userId);
            cmdInsert.Parameters.AddWithValue("content", model.text);
            cmdInsert.Parameters.AddWithValue("createdAt", DateTime.Now);

            await cmdInsert.ExecuteNonQueryAsync();
            model.created_at = DateTime.Now;
            return Json(model);
        }
    }
}
