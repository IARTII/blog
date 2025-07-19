using Npgsql;
using System.Data;
using System.Text.Json;

List<Post> posts = new List<Post>
{
    new Post { id = "1", user_id = "1", title="Отдых", contend="Я сегодня хорошо отдохнул в Египте!", created_at=new DateTime(2025, 7, 19, 9, 21, 0) },
    new Post { id = "2", user_id = "1", title="Еда", contend="Здесь очень вкусная еда!", created_at=new DateTime(2025, 7, 19, 10, 10, 0) }
};

var builder = WebApplication.CreateBuilder();
//builder.Services.AddSingleton<YourNamespace.Postgres.Db>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/posts", () => posts);

app.MapGet("/api/posts/{id}", (string Id) =>
{
    // получаем пользователя по id
    Post? user = posts.FirstOrDefault(u => u.id == Id);
    // если не найден, отправляем статусный код и сообщение об ошибке
    if (user == null) return Results.NotFound(new { message = "Пользователь не найден" });

    // если пользователь найден, отправляем его
    return Results.Json(user);
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

    var userId = Guid.NewGuid().ToString();
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
    //insertCmd.Parameters.AddWithValue("id", Convert.ToInt32(userId));
    insertCmd.Parameters.AddWithValue("username", username);
    insertCmd.Parameters.AddWithValue("password_hash", passwordHash);
    await insertCmd.ExecuteNonQueryAsync();

    return Results.Ok(new { message = "Пользователь зарегистрирован", userId });
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

//app.MapPost("/api/users", (Person user) => {

//    // устанавливаем id для нового пользователя
//    user.Id = Guid.NewGuid().ToString();
//    // добавляем пользователя в список
//    users.Add(user);
//    return user;
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