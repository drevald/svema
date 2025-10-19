using dotenv.net;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Data;
using Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

// add console with custom formatter
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.UseUtcTimestamp = false;
    options.IncludeScopes = false;
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Logging.AddConsole();
builder.Logging.AddFile("Logs/svema-.txt", minimumLevel: LogLevel.Debug);

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

builder.WebHost.ConfigureKestrel(opts =>
{
    opts.ListenAnyIP(Int32.Parse(Environment.GetEnvironmentVariable("PORT")));
    opts.Limits.MaxRequestBodySize = 104857600;
});

builder.Services.AddControllersWithViews();
builder.Services.AddControllers(options =>
{
    options.MaxModelBindingCollectionSize = 5000; // default 1024
});
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
{
    opts.UseNpgsql(dbConnection,npgsqlOptions => npgsqlOptions.CommandTimeout(60));
});


builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "CookieScheme";
    options.DefaultChallengeScheme = "CookieScheme";
})
.AddCookie("CookieScheme", options =>
{
    options.LoginPath = "/login";
})
.AddScheme<AuthenticationSchemeOptions, SharedLinkAuthHandler>("SharedLinkScheme", null)
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = "yourdomain.com",
        ValidAudience = "yourdomain.com",
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("Blessed is he who, in the name of charity and good will shepherds the weak through the valley of darkness")
        )
    };
});

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 10000; // or more, depending on needs
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseStaticFiles();
app.UseRouting();
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Query.ContainsKey("token"))
    {
        var result = await context.AuthenticateAsync("SharedLinkScheme");
        if (result.Succeeded && result.Principal != null)
        {
            context.User = result.Principal;
        }
    }
    await next();
});
app.UseAuthorization();
app.MapControllers();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
}

app.Run();