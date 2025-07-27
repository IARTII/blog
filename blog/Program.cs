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
        posts.Reverse();

        return Results.Json(posts);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message);
    }
});

app.MapGet("/api/comments", async (HttpContext context, IConfiguration config) =>
{
    try
    {
        if (!context.Request.Query.TryGetValue("postId", out var postIdStr) || !int.TryParse(postIdStr, out var postId))
            return Results.BadRequest(new { message = "Некорректный postId" });

        var connectionString = config.GetConnectionString("DefaultConnection");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // Запрос с JOIN, чтобы получить username
        var command = new NpgsqlCommand(@"SELECT users.username, comments.content, comments.created_at FROM comments JOIN users ON users.id = comments.user_id WHERE comments.post_id = @postId ORDER BY comments.created_at ASC", connection);
        command.Parameters.AddWithValue("postId", postId);

        var comments = new List<object>();

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            comments.Add(new
            {
                username = reader.GetString(0),    
                text = reader.GetString(1),         
                created_at = reader.GetDateTime(2)  
            });
        }

        return Results.Json(comments);
    }
    catch (Exception ex)
    {
        return Results.Problem("Ошибка: " + ex.Message);
    }
});

app.MapGet("/api/stats", async (HttpRequest req, IConfiguration config) =>
{
    string? date = req.Query["date"];
    string? userId = req.Query["user_id"];
    string? tag = req.Query["tag"];

    var connectionString = config.GetConnectionString("DefaultConnection");
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    string sql = @"SELECT COUNT(*) FROM posts p LEFT JOIN post_tags pt ON p.id = pt.post_id LEFT JOIN tags t ON t.id = pt.tag_id WHERE 1=1";

    var cmd = new NpgsqlCommand();
    cmd.Connection = conn;

    if (!string.IsNullOrEmpty(date))
    {
        sql += " AND TO_CHAR(p.created_at, 'YYYY-MM-DD') = @date";
        cmd.Parameters.AddWithValue("date", date);
    }

    if (!string.IsNullOrEmpty(userId))
    {
        sql += " AND p.user_id = @userId";
        cmd.Parameters.AddWithValue("userId", int.Parse(userId));
    }

    if (!string.IsNullOrEmpty(tag))
    {
        sql += " AND t.name = @tag";
        cmd.Parameters.AddWithValue("tag", tag);
    }

    cmd.CommandText = sql;

    var result = await cmd.ExecuteScalarAsync();
    int count = Convert.ToInt32(result);

    return Results.Json(new { count });
});

app.MapPost("/api/add_comment", async (HttpContext context, IConfiguration config) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var json = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

        var postId = int.Parse(json["postId"]);
        var username = json["username"];
        var text = json["text"];

        var connectionString = config.GetConnectionString("DefaultConnection");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // Получаем user_id по username
        var cmdGetUserId = new NpgsqlCommand("SELECT id FROM users WHERE username = @username", connection);
        cmdGetUserId.Parameters.AddWithValue("username", username);

        int userId;
        await using (var readerUser = await cmdGetUserId.ExecuteReaderAsync())
        {
            if (!await readerUser.ReadAsync())
                return Results.BadRequest(new { message = "Пользователь не найден" });

            userId = readerUser.GetInt32(0);
        }

        // Добавляем комментарий
        var cmdInsert = new NpgsqlCommand(@"
            INSERT INTO comments (post_id, user_id, content, created_at) 
            VALUES (@post_id, @user_id, @content, @created_at)", connection);

        cmdInsert.Parameters.AddWithValue("post_id", postId);
        cmdInsert.Parameters.AddWithValue("user_id", userId);
        cmdInsert.Parameters.AddWithValue("content", text);
        cmdInsert.Parameters.AddWithValue("created_at", DateTime.Now);

        await cmdInsert.ExecuteNonQueryAsync();

        return Results.Ok(new { message = "Комментарий добавлен" });
    }
    catch (Exception ex)
    {
        return Results.Problem("Ошибка: " + ex.Message);
    }
});

app.MapGet("/api/post/{id}", async (HttpContext context, IConfiguration config, int id) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DefaultConnection");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var command = new NpgsqlCommand("SELECT id, title, content, image_source, created_at FROM posts WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var post = new
            {
                id = reader.GetInt32(0),
                title = reader.GetString(1),
                contend = reader.GetString(2),
                image_url = reader.IsDBNull(3) ? null : reader.GetString(3),
                created_at = reader.GetDateTime(4)
            };

            return Results.Json(post);
        }
        else
        {
            return Results.NotFound(new { message = "Пост не найден" });
        }
    }
    catch (Exception ex)
    {
        return Results.Problem($"Ошибка сервера: {ex.Message}");
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

app.MapPost("/api/add_post", async (HttpContext context, IConfiguration config) =>
{
    var request = context.Request;
    if (!request.HasFormContentType)
        return Results.BadRequest(new { message = "Некорректный тип контента" });

    var form = await request.ReadFormAsync();

    string title = form["title"];
    string content = form["content"];
    string tagsRaw = form["tags"];
    string username = form["username"];

    var image = form.Files.GetFile("image");

    var connectionString = config.GetConnectionString("DefaultConnection");
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    await using var transaction = await connection.BeginTransactionAsync();

    try
    {
        // Получаем user_id
        var getUserCmd = new NpgsqlCommand("SELECT id FROM users WHERE username = @username", connection, transaction);
        getUserCmd.Parameters.AddWithValue("@username", username);
        var userIdObj = await getUserCmd.ExecuteScalarAsync();
        if (userIdObj == null)
            return Results.BadRequest(new { message = "Пользователь не найден" });

        long userId = Convert.ToInt64(userIdObj);

        // Обрабатываем изображение
        string? imagePath = null;
        if (image != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var savePath = Path.Combine("wwwroot", "images", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            await using var stream = File.Create(savePath);
            await image.CopyToAsync(stream);
            imagePath = "/images/" + fileName;
        }

        // Вставляем пост
        var insertPostCmd = new NpgsqlCommand(
            "INSERT INTO posts (user_id, title, content, created_at, image_source) VALUES (@user_id, @title, @content, NOW(), @image) RETURNING id",
            connection, transaction);
        insertPostCmd.Parameters.AddWithValue("@user_id", userId);
        insertPostCmd.Parameters.AddWithValue("@title", title);
        insertPostCmd.Parameters.AddWithValue("@content", content);
        insertPostCmd.Parameters.AddWithValue("@image", (object?)imagePath ?? DBNull.Value);

        var postIdObj = await insertPostCmd.ExecuteScalarAsync();
        long postId = Convert.ToInt64(postIdObj);

        // Обрабатываем теги
        var tags = tagsRaw
            .ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct();

        foreach (var tagName in tags)
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
                // Создаём тег
                var insertTagCmd = new NpgsqlCommand("INSERT INTO tags (name) VALUES (@name) RETURNING id", connection, transaction);
                insertTagCmd.Parameters.AddWithValue("@name", tagName);
                tagId = Convert.ToInt64(await insertTagCmd.ExecuteScalarAsync());
            }

            // Связываем тег с постом
            var insertPostTagCmd = new NpgsqlCommand("INSERT INTO post_tags (post_id, tag_id) VALUES (@postId, @tagId)", connection, transaction);
            insertPostTagCmd.Parameters.AddWithValue("@postId", postId);
            insertPostTagCmd.Parameters.AddWithValue("@tagId", tagId);
            await insertPostTagCmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return Results.Ok(new { message = "Пост добавлен" });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        Console.WriteLine("Ошибка при добавлении поста: " + ex.Message);
        return Results.Problem("Ошибка сервера при добавлении поста.");
    }
});



app.MapPost("/api/liked", async (HttpContext context, IConfiguration config) =>
{
    try
    {
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var json = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

        var postId = int.Parse(json["postId"]);
        var username = json["username"];

        var connectionString = config.GetConnectionString("DefaultConnection");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var getUserCmd = new NpgsqlCommand("SELECT id FROM users WHERE username = @username", conn);
        getUserCmd.Parameters.AddWithValue("username", username);
        var userId = (int?)await getUserCmd.ExecuteScalarAsync();

        if (userId == null)
        {
            return Results.BadRequest("Пользователь не найден");
        }

        var countCmd = new NpgsqlCommand("SELECT COUNT(DISTINCT user_id) FROM likes WHERE post_id = @postId", conn);
        countCmd.Parameters.AddWithValue("postId", postId);
        var likeCount = (long?)await countCmd.ExecuteScalarAsync() ?? 0;

        var likedCmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM likes WHERE user_id = @userId AND post_id = @postId)", conn);
        likedCmd.Parameters.AddWithValue("userId", userId);
        likedCmd.Parameters.AddWithValue("postId", postId);
        var liked = (bool?)await likedCmd.ExecuteScalarAsync() ?? false;

        return Results.Json(new { likeCount, liked });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message);
    }
});

app.MapPost("/api/like_post", async (HttpContext context, IConfiguration config) =>
{
    try
    {
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var json = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

        if (json == null || !json.ContainsKey("postId") || !json.ContainsKey("username"))
            return Results.BadRequest("Неверные данные запроса");

        var postId = int.Parse(json["postId"]);
        var username = json["username"];

        var connectionString = config.GetConnectionString("DefaultConnection");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var getUserCmd = new NpgsqlCommand("SELECT id FROM users WHERE username = @username", conn);
        getUserCmd.Parameters.AddWithValue("username", username);
        var userIdObj = await getUserCmd.ExecuteScalarAsync();

        if (userIdObj == null)
            return Results.BadRequest("Пользователь не найден");

        var userId = (int)userIdObj;

        var checkLikeCmd = new NpgsqlCommand("SELECT 1 FROM likes WHERE user_id = @userId AND post_id = @postId", conn);
        checkLikeCmd.Parameters.AddWithValue("userId", userId);
        checkLikeCmd.Parameters.AddWithValue("postId", postId);
        var exists = await checkLikeCmd.ExecuteScalarAsync() != null;

        if (exists)
        {
            var deleteCmd = new NpgsqlCommand("DELETE FROM likes WHERE user_id = @userId AND post_id = @postId", conn);
            deleteCmd.Parameters.AddWithValue("userId", userId);
            deleteCmd.Parameters.AddWithValue("postId", postId);
            await deleteCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insertCmd = new NpgsqlCommand("INSERT INTO likes (user_id, post_id) VALUES (@userId, @postId)", conn);
            insertCmd.Parameters.AddWithValue("userId", userId);
            insertCmd.Parameters.AddWithValue("postId", postId);
            await insertCmd.ExecuteNonQueryAsync();
        }

        var countCmd = new NpgsqlCommand("SELECT COUNT(DISTINCT user_id) FROM likes WHERE post_id = @postId", conn);
        countCmd.Parameters.AddWithValue("postId", postId);
        var likeCount = (long?)await countCmd.ExecuteScalarAsync() ?? 0;

        var liked = !exists;

        return Results.Json(new { likeCount, liked });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message);
    }
});

app.MapPost("/api/logout", async (HttpContext context, IConfiguration config) =>
{
    try
    {
        context.Response.Cookies.Delete("CookieAuthBlog");

        return Results.Ok(new { message = "Вы успешно вышли из аккаунта" });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message);
    }
});

app.MapGet("/api/tags", async (HttpContext context, IConfiguration config) =>
{
    if (!context.Request.Query.TryGetValue("postId", out var postIdStr) || !int.TryParse(postIdStr, out var postId))
        return Results.BadRequest(new { message = "Некорректный postId" });

    var tags = new List<string>();
    try
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var command = new NpgsqlCommand(@"SELECT t.name FROM post_tags pt JOIN tags t ON t.id = pt.tag_id WHERE pt.post_id = @postId", connection);

        command.Parameters.AddWithValue("@postId", postId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tags.Add(reader.GetString(0));
        }

        return Results.Ok(tags);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Ошибка при получении тегов: " + ex.Message);
        return Results.Problem("Ошибка сервера при получении тегов");
    }
});

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

public class Like
{
    public string likeCount { get; set; }
    public bool liked { get; set; }
}