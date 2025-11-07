using Form;
using Npgsql;
using Data;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.IO;
using Utils;

namespace Services;

public class ShotService : Service
{

    public ShotService(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public Shot GetShot(int id)
    {
        return dbContext.Shots.Find(id);
    }

    public Shot GetShotWithStorage(int id)
    {
        return dbContext.Shots
            .Where(s => s.ShotId == id)
            .Include(s => s.Storage)
            .FirstOrDefault();
    }

    public Shot GetShotWithComments(int id)
    {
        return dbContext.Shots
            .Include(s => s.ShotComments)
            .Include(s => s.Album)
                .ThenInclude(a => a.User)
            .FirstOrDefault(s => s.ShotId == id);
    }

    public Shot GetAuthorizedShot(int id, int? currentUserId, string token)
    {
        return dbContext.Shots
            .Include(s => s.ShotComments)
            .Include(s => s.Storage)
            .Include(s => s.Album)
                .ThenInclude(a => a.User)
            .FirstOrDefault(s =>
                s.ShotId == id &&
                (
                    // Shot is owned by current user
                    s.Album.User.UserId == currentUserId ||

                    // Album is shared directly to this user
                    dbContext.SharedAlbums.Any(sa =>
                        sa.AlbumId == s.Album.AlbumId &&
                        sa.GuestUserId == currentUserId
                    ) ||

                    // Album belongs to a host user who shared their library
                    dbContext.SharedUsers.Any(su =>
                        su.HostUserId == s.Album.User.UserId &&
                        su.GuestUserId == currentUserId
                    ) ||

                    // Shot (or album) is shared via public link
                    dbContext.SharedLinks.Any(sl =>
                        (
                            (sl.ResourceType == "shot" && sl.ResourceId == s.ShotId) ||
                            (sl.ResourceType == "album" && sl.ResourceId == s.Album.AlbumId)
                        ) &&
                        !sl.Revoked &&
                        (sl.ExpiresAt == null || sl.ExpiresAt > DateTime.UtcNow) &&
                        sl.Token == token
                    )
                )
            );
    }

    public byte[] GetShotPreview(int id)
    {
        var shot = dbContext.Shots
            .Where(s => s.ShotId == id)
            .Select(s => new { s.Preview })
            .FirstOrDefault();

        return shot?.Preview;
    }

    public List<Shot> GetShotsByAlbum(int albumId)
    {
        return dbContext.Shots.Where(s => s.AlbumId == albumId).ToList();
    }

    public List<int> GetShotIdsByAlbum(int albumId)
    {
        return dbContext.Shots
            .Where(s => s.AlbumId == albumId)
            .Select(s => s.ShotId)
            .ToList();
    }

    public List<ShotPreviewDTO> GetShotPreviews(int albumId, double west, double east, double south, double north)
    {
        return dbContext.Shots
            .Where(s => s.AlbumId == albumId && s.Longitude > west && s.Longitude < east && s.Latitude > south && s.Latitude < north)
            .OrderBy(s => s.ShotId)
            .Select(s => new ShotPreviewDTO
            {
                ShotId = s.ShotId,
                Name = s.Name,
                SourceUri = s.SourceUri,
                Flip = s.Flip,
                Rotate = s.Rotate
            })
            .ToList();
    }

    public Shot GetNextShot(int albumId, int currentShotId)
    {
        return dbContext.Shots
            .Where(s => s.AlbumId == albumId && s.ShotId > currentShotId)
            .OrderBy(s => s.ShotId)
            .FirstOrDefault();
    }

    public Shot GetPreviousShot(int albumId, int currentShotId)
    {
        return dbContext.Shots
            .Where(s => s.AlbumId == albumId && s.ShotId < currentShotId)
            .OrderBy(s => s.ShotId)
            .LastOrDefault();
    }

    public void UpdateShot(ShotDTO dto)
    {
        var shot = dbContext.Shots.Find(dto.ShotId);
        if (shot == null) return;

        shot.Name = dto.Name;
        shot.DateStart = dto.DateStart;
        shot.DateEnd = dto.DateEnd;
        shot.Longitude = dto.Longitude;
        shot.Latitude = dto.Latitude;
        shot.Zoom = dto.Zoom;

        dbContext.SaveChanges();
    }

    public void UpdateShotAsAlbumPreview(int shotId, int albumId)
    {
        var album = dbContext.Albums.Find(albumId);
        if (album != null)
        {
            album.PreviewId = shotId;
            dbContext.SaveChanges();
        }
    }

    public void BulkUpdateShots(List<int> shotIds, AlbumDTO dto)
    {
        if (!shotIds.Any()) return;

        int chunkSize = 1000;
        var shotIdsArray = shotIds.ToArray();

        for (int i = 0; i < shotIdsArray.Length; i += chunkSize)
        {
            var chunk = shotIdsArray.Skip(i).Take(chunkSize).ToArray();
            var idParams = string.Join(",", chunk);

            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] START UPDATE CHUNK {i / chunkSize + 1}");

            var updateFields = new List<string>();
            var parameters = new List<object>();
            var paramIndex = 0;

            if (dto.Year < 0)
            {
                updateFields.Add($"DateStart = {{{paramIndex++}}}, DateEnd = {{{paramIndex++}}}");
                parameters.Add(DateTime.MinValue);
                parameters.Add(DateTime.MinValue);
            }
            else
            {
                if (DateTime.MinValue != dto.DateStart)
                {
                    updateFields.Add($"DateStart = {{{paramIndex++}}}");
                    parameters.Add(dto.DateStart);
                }
                if (DateTime.MinValue != dto.DateEnd)
                {
                    updateFields.Add($"DateEnd = {{{paramIndex++}}}");
                    parameters.Add(dto.DateEnd);
                }
            }

            if (dto.Longitude != 0 && dto.Latitude != 0)
            {
                updateFields.Add($"Latitude = {{{paramIndex++}}}, Longitude = {{{paramIndex++}}}, Zoom = {{{paramIndex++}}}");
                parameters.Add(dto.Latitude);
                parameters.Add(dto.Longitude);
                parameters.Add(dto.Zoom);
            }

            var flipRotateUpdates = dto.Shots
                .Where(s => s != null && shotIds.Contains(s.ShotId))
                .GroupBy(s => new { s.Flip, s.Rotate })
                .ToList();

            foreach (var group in flipRotateUpdates)
            {
                var groupShotIds = group.Select(s => s.ShotId).ToArray();
                var groupIdParams = string.Join(",", groupShotIds);

                var groupUpdateFields = new List<string>(updateFields);
                groupUpdateFields.Add($"Flip = {{{paramIndex}}}, Rotate = {{{paramIndex + 1}}}");

                var groupParameters = new List<object>(parameters);
                groupParameters.Add(group.Key.Flip);
                groupParameters.Add(group.Key.Rotate);

                var sql = $"UPDATE Shots SET {string.Join(", ", groupUpdateFields)} WHERE Id IN ({groupIdParams})";

                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] EXECUTING SQL FOR {groupShotIds.Length} SHOTS");
                dbContext.Database.ExecuteSqlRaw(sql, groupParameters.ToArray());
            }

            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] END UPDATE CHUNK {i / chunkSize + 1}");
        }
    }

    public void BulkUpdateShotsLocation(List<int> shotIds, double latitude, double longitude, int zoom)
    {
        if (!shotIds.Any()) return;

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SAVE SHOTS");
        int chunkSize = 1000;
        var shotIdsArray = shotIds.ToArray();

        for (int i = 0; i < shotIdsArray.Length; i += chunkSize)
        {
            var chunk = shotIdsArray.Skip(i).Take(chunkSize).ToArray();
            var idParams = string.Join(",", chunk);

            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET LAT = {latitude}");
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET LON = {longitude}");
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET ZOOM = {zoom}");
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS START UPDATE {idParams}");

            dbContext.Database.ExecuteSqlRaw(
                $"UPDATE Shots SET Latitude = {{0}}, Longitude = {{1}}, Zoom = {{2}} WHERE Id IN ({idParams})",
                latitude, longitude, zoom
            );

            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS END UPDATE");
        }
    }

    public void MoveShots(List<int> shotIds, int sourceAlbumId, int targetAlbumId)
    {
        var shots = dbContext.Shots
            .Where(s => s.AlbumId == sourceAlbumId && shotIds.Contains(s.ShotId))
            .ToList();

        foreach (var shot in shots)
            shot.AlbumId = targetAlbumId;

        dbContext.SaveChanges();
    }

    public async Task DeleteShot(int id)
    {
        var shot = dbContext.Shots.Where(s => s.ShotId == id).Include(s => s.Storage).FirstOrDefault();
        if (shot == null) return;

        await Storage.DeleteFile(shot);
        dbContext.Remove(shot);
        dbContext.SaveChanges();
    }

    //todo - return ids only
    public List<Shot> GetUncommentedShots(int userId, string username, int offset, int size)
    {
        var query = dbContext.Shots
            .Where(shot =>
                (shot.Album.User.UserId == userId && shot.Album.User.Username == username) ||
                dbContext.SharedUsers.Any(su =>
                    su.GuestUser.Username == username &&
                    su.HostUser.UserId == shot.Album.User.UserId) ||
                dbContext.SharedAlbums.Any(sa =>
                    sa.GuestUser.Username == username &&
                    sa.Album.AlbumId == shot.Album.AlbumId)
            )
            .Where(shot =>
                !shot.ShotComments.Any(c => c.Author.Username == username)
            );

        return query
            .OrderByDescending(s => s.ShotId)
            .Skip(offset)
            .Take(size)
            .ToList();
    }

    public List<Shot> GetAllShots()
    {
        return dbContext.Shots.ToList();
    }

    public Shot GetShotWithAlbumAndUser(int shotId, int currentUserId)
    {
        return dbContext.Shots
            .Include(s => s.Album)
            .ThenInclude(a => a.User)
            .FirstOrDefault(s => s.ShotId == shotId && s.Album.User.UserId == currentUserId);
    }

    public List<ShotPreviewDTO> GetSameDayShots(int month, int day, int tolerance)
    {
        // Compute target day-of-year using an arbitrary non-leap year (e.g., 2025)
        int targetDayOfYear = new DateTime(2025, month, day).DayOfYear;

       var shots = dbContext.Shots
        .FromSqlInterpolated($@"
            SELECT *
            FROM shots
            WHERE date_start IS NOT NULL
            AND date_start NOT IN ('infinity', '-infinity')
            AND EXTRACT(doy FROM date_start) - {targetDayOfYear} <= {tolerance}
            AND EXTRACT(doy FROM date_start) - {targetDayOfYear} >= -{tolerance}")
        .Select(s => new ShotPreviewDTO
        {
            ShotId = s.ShotId,
            Name = s.Name,
            SourceUri = s.SourceUri,
            Flip = s.Flip,
            Rotate = s.Rotate,
            DateStart = s.DateStart
        })
        .ToList();

        return shots;
    }
}