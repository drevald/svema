using dotenv.net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using svema.Data;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
var config = builder.Configuration;

var connectionString = config["DB_CONNECTION"];
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.ListenAnyIP(7777);
});
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
{
    opts.UseNpgsql(connectionString);
});
var app = builder.Build();
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});
app.Run();