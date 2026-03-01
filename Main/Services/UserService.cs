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

    // Shared Users methods
    public List<SharedUser> GetSharedUsers(int hostUserId)
    {
        return dbContext.SharedUsers
            .Include(su => su.GuestUser)
            .Where(su => su.HostUserId == hostUserId)
            .ToList();
    }

    public List<SharedUser> GetHostsWhoSharedWithMe(int guestUserId)
    {
        return dbContext.SharedUsers
            .Include(su => su.HostUser)
            .Where(su => su.GuestUserId == guestUserId)
            .ToList();
    }

    public SharedUser AddSharedUser(int hostUserId, int guestUserId)
    {
        var existing = dbContext.SharedUsers
            .FirstOrDefault(su => su.HostUserId == hostUserId && su.GuestUserId == guestUserId);

        if (existing != null)
            return existing;

        var sharedUser = new SharedUser
        {
            HostUserId = hostUserId,
            GuestUserId = guestUserId
        };
        dbContext.SharedUsers.Add(sharedUser);
        dbContext.SaveChanges();
        return sharedUser;
    }

    public bool RemoveSharedUser(int sharedUserId, int hostUserId)
    {
        var sharedUser = dbContext.SharedUsers
            .FirstOrDefault(su => su.Id == sharedUserId && su.HostUserId == hostUserId);

        if (sharedUser == null)
            return false;

        dbContext.SharedUsers.Remove(sharedUser);
        dbContext.SaveChanges();
        return true;
    }

    public bool? ToggleSharedUserDisabled(int sharedUserId, int guestUserId)
    {
        var sharedUser = dbContext.SharedUsers
            .FirstOrDefault(su => su.Id == sharedUserId && su.GuestUserId == guestUserId);

        if (sharedUser == null)
            return null;

        sharedUser.DisabledByGuest = !sharedUser.DisabledByGuest;
        dbContext.SaveChanges();
        return sharedUser.DisabledByGuest;
    }

    public List<User> SearchUsers(string query, int excludeUserId)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<User>();

        return dbContext.Users
            .Where(u => u.UserId != excludeUserId &&
                       (u.Username.ToLower().Contains(query.ToLower()) ||
                        (u.Email != null && u.Email.ToLower().Contains(query.ToLower()))))
            .Take(10)
            .ToList();
    }

}