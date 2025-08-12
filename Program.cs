var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication("CookieAuthBlog")
    .AddCookie("CookieAuthBlog", options =>
    {
        options.Cookie.Name = "CookieAuthBlog";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddControllers();
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
