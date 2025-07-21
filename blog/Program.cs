using Npgsql;
using System;
using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

List<Post> posts = new List<Post>
{
    new Post { id = "1", user_id = "1", title="Отдых", contend="Я сегодня хорошо отдохнул в Египте!", created_at=new DateTime(2025, 7, 19, 9, 21, 0) },
    new Post { id = "2", user_id = "1", title="Еда", contend="Здесь очень вкусная еда!", created_at=new DateTime(2025, 7, 19, 10, 10, 0) }
};

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

app.MapGet("/api/posts", [Authorize] () => posts);

app.MapGet("/api/posts/{id}", [Authorize] async (string Id, IConfiguration config) =>
{
    var connectionString = config.GetConnectionString("DefaultConnection");
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    var checkCmd = new NpgsqlCommand("SELECT * FROM posts", conn);
    Post posts = (Post)(await checkCmd.ExecuteScalarAsync());
    if (posts == null)
        return Results.Conflict(new { message = "Постов пока нет!" });

    Post? post = posts;

    return Results.Json(post);
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

    // Вставка
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
            new Claim(ClaimTypes.Name, username), // имя пользователя
            //new Claim("MyCustomClaim", "CustomValue") // можно своё что-то добавить
        };
        var identity = new ClaimsIdentity(claims, "CookieAuthBlog"); // создаём "удостоверение"
        var principal = new ClaimsPrincipal(identity);             // "личность" пользователя

        await context.SignInAsync("CookieAuthBlog", principal);

        return Results.Ok(new { message = "Пользователь вошел" });
    }
    return Results.NotFound(new { message = "Неверный пароль" });
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

//app.MapPut("/api/users", (Person userData) => {

//    // получаем пользователя по id
//    var user = users.FirstOrDefault(u => u.Id == userData.Id);
//    // если не найден, отправляем статусный код и сообщение об ошибке
//    if (user == null) return Results.NotFound(new { message = "Пользователь не найден" });
//    // если пользователь найден, изменяем его данные и отправляем обратно клиенту

//    user.Age = userData.Age;
//    user.Name = userData.Name;
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
}

public class User
{
    public string id { get; set; }
    public string username { get; set; }
    public string password_hash { get; set; }
}