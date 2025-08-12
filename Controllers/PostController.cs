using Blogs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

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
            var connectionString = _config.GetConnectionString("DefaultConnection");
            var posts = new List<Post>();

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("SELECT id, user_id, title, content, created_at, image_source FROM posts", conn);
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
                    image_url = reader["image_source"] == DBNull.Value ? null : reader["image_source"].ToString()
                });
            }

            posts.Reverse();

            return View(posts);
        }

        // GET: PostController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }
    }
}
