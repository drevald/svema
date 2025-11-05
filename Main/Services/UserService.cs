using Form;
using Npgsql;
using Data;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Utils;
using System.Security.Claims;
using Services;
using Microsoft.AspNetCore.Http;

namespace Services;

public class UserService : Service
{

    public UserService(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public string GetUsername(HttpContext HttpContext)
    {
        return HttpContext.User?.FindFirst(ClaimTypes.Name)?.Value;
    }

    public int? GetUserId(HttpContext HttpContext)
    {
        var idValue = HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(idValue, out var id))
            return id;

        return null;
    }

    public User GetUserByUsername(string username)
    {
        return dbContext.Users.FirstOrDefault(u => u.Username == username);
    }

    public User GetUserById(int userId)
    {
        return dbContext.Users.FirstOrDefault(u => u.UserId == userId);
    }

    public ShotStorage GetStorageForUser(int userId)
    {
        return dbContext.ShotStorages.FirstOrDefault(s => s.User != null && s.User.UserId == userId);
    }

    public ShotStorage GetStorageById(int storageId)
    {
        return dbContext.ShotStorages.Where(s => s.Id == storageId).FirstOrDefault();
    }

    public List<ShotStorage> GetStoragesForUser(int userId)
    {
        return dbContext.ShotStorages.Where(s => s.User != null && s.User.UserId == userId).ToList();
    }

    public void AddOrUpdateStorage(ShotStorage storage)
    {
        dbContext.AddOrUpdateEntity(storage);
        dbContext.SaveChanges();
    }

}