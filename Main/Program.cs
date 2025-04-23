using dotenv.net;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

using Data;

var logger = LoggerFactory.Create(config => {
    config.AddConsole();
}).CreateLogger("Program");

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
var config = builder.Configuration;

var uri = new Uri(Environment.GetEnvironmentVariable("DATABASE_URL"));
var username = uri.UserInfo.Split(':')[0];
var password = uri.UserInfo.Split(':')[1];
var dbConnection =
"Host=" + uri.Host +
";Database=" + uri.AbsolutePath.Substring(1) +
";Username=" + username +
";Password=" + password + 
";Port=" + uri.Port;

dbConnection += ";Include Error Detail=True";

builder.WebHost.ConfigureKestrel(opts => {
    opts.ListenAnyIP(Int32.Parse(Environment.GetEnvironmentVariable("PORT")));
    opts.Limits.MaxRequestBodySize = 104857600;
});

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
{
    opts.UseNpgsql(dbConnection);
    
});
builder.Services.AddAuthentication("CookieScheme")
    .AddCookie("CookieScheme", options => {
        options.AccessDeniedPath = "/denied";
        options.LoginPath = "/login";
    });

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


using(var scope = app.Services.CreateScope())
{
    try {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
    } catch (Exception e) {
        Console.WriteLine(e.Message);
    }
}

app.Run();