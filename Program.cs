﻿using dotenv.net;
using System;
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

var uri = new Uri(Environment.GetEnvironmentVariable("DATABASE_URL"));
var username = uri.UserInfo.Split(':')[0];
var password = uri.UserInfo.Split(':')[1];
var dbConnection =
"Host=" + uri.Host +
";Database=" + uri.AbsolutePath.Substring(1) +
";Username=" + username +
";Password=" + password + 
";Port=" + uri.Port;

Environment.SetEnvironmentVariable ("DB_CONNECTION", dbConnection);
var connectionString = config["DB_CONNECTION"];
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.ListenAnyIP(Int32.Parse(Environment.GetEnvironmentVariable("PORT")));
});
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
{
    opts.UseNpgsql(dbConnection);
});
var app = builder.Build();
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});
app.UseAuthentication();
app.Run();