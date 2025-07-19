List<Post> posts = new List<Post>
{
    new Post { id = "1", user_id = "1", title="ќтдых", contend="я сегодн€ хорошо отдохнул в ≈гипте!", created_at=new DateTime(2025, 7, 19, 9, 21, 0) },
    new Post { id = "2", user_id = "1", title="≈да", contend="«десь очень вкусна€ еда!", created_at=new DateTime(2025, 7, 19, 10, 10, 0) }
};

var builder = WebApplication.CreateBuilder();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/posts", () => posts);

app.MapGet("/api/posts/{id}", (string Id) =>
{
    // получаем пользовател€ по id
    Post? user = posts.FirstOrDefault(u => u.id == Id);
    // если не найден, отправл€ем статусный код и сообщение об ошибке
    if (user == null) return Results.NotFound(new { message = "ѕользователь не найден" });

    // если пользователь найден, отправл€ем его
    return Results.Json(user);
});

app.MapPost("/api/registration", (User user) =>
{
    // устанавливаем id дл€ нового пользовател€
    user.id = Guid.NewGuid().ToString();
    // добавл€ем пользовател€ в список
    //users.Add(user);
    return user;
});

//app.MapDelete("/api/users/{id}", (string id) =>
//{
//    // получаем пользовател€ по id
//    Person? user = users.FirstOrDefault(u => u.Id == id);

//    // если не найден, отправл€ем статусный код и сообщение об ошибке
//    if (user == null) return Results.NotFound(new { message = "ѕользователь не найден" });

//    // если пользователь найден, удал€ем его
//    users.Remove(user);
//    return Results.Json(user);
//});

//app.MapPost("/api/users", (Person user) => {

//    // устанавливаем id дл€ нового пользовател€
//    user.Id = Guid.NewGuid().ToString();
//    // добавл€ем пользовател€ в список
//    users.Add(user);
//    return user;
//});

//app.MapPut("/api/users", (Person userData) => {

//    // получаем пользовател€ по id
//    var user = users.FirstOrDefault(u => u.Id == userData.Id);
//    // если не найден, отправл€ем статусный код и сообщение об ошибке
//    if (user == null) return Results.NotFound(new { message = "ѕользователь не найден" });
//    // если пользователь найден, измен€ем его данные и отправл€ем обратно клиенту

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