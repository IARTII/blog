using Blogs.Domain.Services;
using Blogs.Service;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication("CookieAuthBlog")
    .AddCookie("CookieAuthBlog", options =>
    {
        options.Cookie.Name = "CookieAuthBlog";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10, 
        shared: true 
    )
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<Blogs.Filters.CustomExceptionFilter>();
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICommentsService, CommentsService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Index}/{id?}");

app.Run();
