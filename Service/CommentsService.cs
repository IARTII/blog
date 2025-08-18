using Blogs.Domain.Services;
using Blogs.Models;
using Blogs.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Blogs.Service
{
    public class CommentsService : ICommentsService
    {
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CommentsService(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<(bool Success, string Message, PostWithComments Model)> Comments(int? postId)
        {
            if (postId == null)
                return (false, "postId не передан", null);

            var post = await GetPostAsync(postId.Value);
            if (post == null)
                return (false, "Пост не найден", null);

            var comments = await GetCommentsAsync(postId.Value);

            var httpContext = _httpContextAccessor.HttpContext;

            var model = new PostWithComments
            {
                Post = post,
                Comments = comments,
                CurrentUsername = httpContext.User.Identity?.Name ?? "Гость"
            };

            return (true, "Ок", model);
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

        public async Task<(bool Success, string Message, Comment Model)> AddComment(Comment model)
        {
            if (string.IsNullOrWhiteSpace(model.text))
                return (false, "Текст комментария обязателен", null);

            var connStr = _config.GetConnectionString("DefaultConnection");
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            var cmdUser = new NpgsqlCommand("SELECT id FROM users WHERE username=@username", conn);
            cmdUser.Parameters.AddWithValue("username", model.username);

            int userId;
            await using (var reader = await cmdUser.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    return (false, "Пользователь не найден", null);

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

            return (true, "Комментарий добавлен", model);
        }

    }
}
