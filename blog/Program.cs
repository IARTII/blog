using Npgsql;
using System;
using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Serialization;

List<Post> posts = new List<Post>();

var builder = WebApplication.CreateBuilder();
//builder.Services.AddSingleton<YourNamespace.Postgres.Db>();
builder.Services.AddAuthentication("CookieAuthBlog")
    .AddCookie("CookieAuthBlog", options =>
    {
        options.Cookie.Name = "CookieAuthBlog";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/posts", [Authorize] async (IConfiguration config) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("SELECT id, user_id, title, content, created_at, image_source FROM posts", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var posts = new List<Post>();

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

        return Results.Json(posts);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message);
    }
});


app.MapGet("/api/Iregistrate", (HttpContext context, IConfiguration config) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect("/blog.html");
    }
    return Results.File("wwwroot/index.html", "text/html");
});

app.MapPost("/api/registration", async (HttpContext context, IConfiguration config) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    var json = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

    if (json == null || !json.ContainsKey("username") || !json.ContainsKey("password"))
        return Results.BadRequest(new { message = "Неверный формат запроса" });

    var username = json["username"];
    var password = json["password"];

    var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

    var connectionString = config.GetConnectionString("DefaultConnection");
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    // Проверка на существование пользователя
    var checkCmd = new NpgsqlCommand("SELECT 1 FROM users WHERE username = @username", conn);
    checkCmd.Parameters.AddWithValue("username", username);
    var exists = await checkCmd.ExecuteScalarAsync();
    if (exists != null)
        return Results.Conflict(new { message = "Пользователь уже существует." });

    // Создание пользователя
    var insertCmd = new NpgsqlCommand(
        "INSERT INTO users (username, password_hash) VALUES (@username, @password_hash)", conn);
    insertCmd.Parameters.AddWithValue("username", username);
    insertCmd.Parameters.AddWithValue("password_hash", passwordHash);
    await insertCmd.ExecuteNonQueryAsync();

    return Results.Ok(new { message = "Пользователь зарегистрирован"});
});

app.MapPost("/api/login", async (HttpContext context, IConfiguration config) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    var json = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

    if (json == null || !json.ContainsKey("username") || !json.ContainsKey("password"))
        return Results.BadRequest(new { message = "Неверный формат запроса" });

    var username = json["username"];
    var password = json["password"];

    var connectionString = config.GetConnectionString("DefaultConnection");
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    // Проверка на существование пользователя
    var checkCmd = new NpgsqlCommand("SELECT username FROM users WHERE username = @username", conn);
    checkCmd.Parameters.AddWithValue("username", username);
    var exists = await checkCmd.ExecuteScalarAsync();
    if (exists == null)
        return Results.Conflict(new { message = "Пользователь не существует." });

    // Поиск хэша пароля
    var findCmd = new NpgsqlCommand(
        "SELECT password_hash FROM users WHERE username = @username", conn);
    findCmd.Parameters.AddWithValue("username", username);
    var storedHash = await findCmd.ExecuteScalarAsync();

    if (BCrypt.Net.BCrypt.Verify(password, storedHash.ToString()))
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
        };
        var identity = new ClaimsIdentity(claims, "CookieAuthBlog");
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync("CookieAuthBlog", principal);

        return Results.Ok(new { message = "Пользователь вошел" });
    }
    return Results.NotFound(new { message = "Неверный пароль" });
});

app.MapPost("/api/add_post", async (HttpRequest request, IConfiguration config) =>
{
    var form = await request.ReadFormAsync();

    var title = form["title"];
    var content = form["content"];
    var username = form["username"];
    var createdAt = DateTime.UtcNow;
    var file = form.Files["image"];
    string? imageUrl = null;

    if (file != null && file.Length > 0)
    {
        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
        Directory.CreateDirectory(uploadFolder);

        var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadFolder, uniqueFileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        imageUrl = "/images/" + uniqueFileName;
    }

    var connectionString = config.GetConnectionString("DefaultConnection");
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    // Ищем id пользователя
    var cmdUser = new NpgsqlCommand("SELECT id FROM users WHERE username = @username", conn);
    cmdUser.Parameters.AddWithValue("username", username.ToString());
    var userId = await cmdUser.ExecuteScalarAsync();

    if (userId == null)
        return Results.BadRequest(new { message = "Пользователь не найден." });

    var cmdInsert = new NpgsqlCommand(@"
        INSERT INTO posts (user_id, title, content, created_at, image_source)
        VALUES (@user_id, @title, @content, @created_at, @image_url)", conn);

    cmdInsert.Parameters.AddWithValue("user_id", userId);
    cmdInsert.Parameters.AddWithValue("title", title.ToString());
    cmdInsert.Parameters.AddWithValue("content", content.ToString());
    cmdInsert.Parameters.AddWithValue("created_at", createdAt);
    cmdInsert.Parameters.AddWithValue("image_url", (object?)imageUrl ?? DBNull.Value);

    await cmdInsert.ExecuteNonQueryAsync();

    return Results.Ok(new { message = "Пост добавлен!" });
});

//app.MapDelete("/api/users/{id}", (string id) =>
//{
//    // получаем пользователя по id
//    Person? user = users.FirstOrDefault(u => u.Id == id);

//    // если не найден, отправляем статусный код и сообщение об ошибке
//    if (user == null) return Results.NotFound(new { message = "Пользователь не найден" });

//    // если пользователь найден, удаляем его
//    users.Remove(user);
//    return Results.Json(user);
//});

app.Run();

public class Post
{
    public string id { get; set; }
    public string user_id { get; set; }
    public string title { get; set; }
    public string contend { get; set; }
    public DateTime created_at { get; set; }
    public string? image_url { get; set; }
}
