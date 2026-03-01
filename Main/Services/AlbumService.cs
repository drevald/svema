using Form;
using Npgsql;
using Data;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq;
using System;
using System.Globalization;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Utils;
using Common;
using Services;


namespace Services;

public class AlbumService : Service
{

    public AlbumService(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public Album GetAuthorizedAlbum(int id, int? currentUserId, string token)
    {
        // Get list of host user IDs that shared their library with current user
        var sharedHostIds = currentUserId.HasValue
            ? dbContext.SharedUsers
                .Where(su => su.GuestUserId == currentUserId.Value)
                .Select(su => su.HostUserId)
                .ToList()
            : new List<int>();

        // Get list of album IDs shared directly with current user
        var sharedAlbumIds = currentUserId.HasValue
            ? dbContext.SharedAlbums
                .Where(sa => sa.GuestUserId == currentUserId.Value)
                .Select(sa => sa.AlbumId)
                .ToList()
            : new List<int>();

        return dbContext.Albums
            .Include(a => a.AlbumComments)
            .FirstOrDefault(a =>
                a.AlbumId == id &&
                (
                    // 1️⃣ Owned by current user
                    a.User.UserId == currentUserId ||

                    // 2️⃣ Shared directly to this user
                    sharedAlbumIds.Contains(a.AlbumId) ||

                    // 3️⃣ Shared via host-user relationship
                    sharedHostIds.Contains(a.User.UserId) ||

                    // 4️⃣ Public shared link (still active)
                    dbContext.SharedLinks.Any(sl =>
                        sl.ResourceType == "album" &&
                        sl.ResourceId == a.AlbumId &&
                        !sl.Revoked &&
                        sl.Token == token &&
                        (sl.ExpiresAt == null || sl.ExpiresAt > DateTime.UtcNow)
                    )
                )
            );
    }

    public void UpdateAlbums(AlbumsListDTO dto, List<int> shotsToChange)
    {
        if (dto.EditLocation && shotsToChange.Any())
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SAVE SHOTS");
            int chunkSize = 1000;
            var shotIds = shotsToChange.ToArray();
            for (int i = 0; i < shotIds.Length; i += chunkSize)
            {
                var chunk = shotIds.Skip(i).Take(chunkSize).ToArray();
                var idParams = string.Join(",", chunk);
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET LAT = " + dto.Latitude);
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET LON = " + dto.Longitude);
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET ZOOM = " + dto.Zoom);
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS START UPDATE " + idParams);
                dbContext.Database.ExecuteSqlRaw(
                    $"UPDATE Shots SET Latitude = {{0}}, Longitude = {{1}}, Zoom = {{2}} WHERE Id IN ({idParams})",
                    dto.Latitude, dto.Longitude, dto.Zoom
                );
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS END UPDATE");
            }
        }
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS START SAVE CHANGES");
        dbContext.SaveChanges();
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS END SAVE CHANGES");
    }

    public AlbumsListDTO BuildAlbumsListAsync(AlbumsListDTO dto, String username, bool onlyMine)
    {
        dto ??= new AlbumsListDTO();


        var filteredShots = ApplyShotFilters(dto, username, onlyMine);
        var shotGroups = filteredShots
            .GroupBy(s => s.AlbumId)
            .Select(g => new
            {
                AlbumId = g.Key,
                Size = g.Count(),
                EarliestDate = g.Min(s => s.DateStart),
                LeastLatitude = g.Min(s => s.Latitude),
                LeastLongitude = g.Min(s => s.Longitude)
            })
            .ToList();

        var albumIds = shotGroups.Select(g => g.AlbumId).ToList();
        var albumCards = dbContext.Albums
            .Include(a => a.PreviewShot)
            .Where(a => albumIds.Contains(a.AlbumId))
            .Select(a => new AlbumCardDTO
            {
                AlbumId = a.AlbumId,
                Name = a.Name,
                PreviewId = a.PreviewId ?? 0,
                PreviewFlip = a.PreviewShot != null && a.PreviewShot.Flip,
                PreviewRotate = a.PreviewShot != null ? a.PreviewShot.Rotate : 0
            })
            .ToList();

        var enriched = albumCards
            .Join(shotGroups,
                card => card.AlbumId,
                group => group.AlbumId,
                (card, group) => new
                {
                    Card = card,
                    group.Size,
                    group.EarliestDate,
                    group.LeastLatitude,
                    group.LeastLongitude
                });

        var sortTuple = (dto.SortBy, dto.SortDirection);

        var sorted = sortTuple switch
        {
            (SortBy.EarliestDate, SortDirection.Ascending) => enriched.OrderBy(e => e.EarliestDate),
            (SortBy.EarliestDate, SortDirection.Descending) => enriched.OrderByDescending(e => e.EarliestDate),
            (SortBy.LeastLatitude, SortDirection.Ascending) => enriched.OrderBy(e => e.LeastLatitude),
            (SortBy.LeastLatitude, SortDirection.Descending) => enriched.OrderByDescending(e => e.LeastLatitude),
            (SortBy.LeastLongitude, SortDirection.Ascending) => enriched.OrderBy(e => e.LeastLongitude),
            (SortBy.LeastLongitude, SortDirection.Descending) => enriched.OrderByDescending(e => e.LeastLongitude),
            (SortBy.ShotCount, SortDirection.Ascending) => enriched.OrderBy(e => e.Size),
            (SortBy.ShotCount, SortDirection.Descending) => enriched.OrderByDescending(e => e.Size),
            _ => enriched.OrderBy(e => e.EarliestDate)
        };

        var finalAlbumCards = sorted
            .Select(e =>
            {
                e.Card.Size = e.Size;
                return e.Card;
            })
            .ToList();

        if (onlyMine)
        {
            var emptyAlbums = dbContext.Albums
                .Where(a => a.User != null && a.User.Username == username)
                .Where(a => !dbContext.Shots.Any(s => s.AlbumId == a.AlbumId))
                .ToList();

            var emptyAlbumCards = emptyAlbums
                .Select(a => new AlbumCardDTO
                {
                    AlbumId = a.AlbumId,
                    Name = a.Name,
                    PreviewId = -1,
                    PreviewFlip = false,
                    PreviewRotate = 0
                })
                .ToList();

            finalAlbumCards.AddRange(emptyAlbumCards);
        }

        var cameras = dbContext.Shots.Select(s => s.CameraModel).Distinct().ToList();
        var locations = dbContext.Locations.OrderBy(l => l.Name).ToList();
        // var placemarks = locationService.GetClusteredShotsWithLabels(username, onlyMine, dto.West, dto.East, dto.South, dto.North);
        // var rect = GeoRect.FromPlacemarks(placemarks, 0.1);
        return new AlbumsListDTO
        {
            Albums = finalAlbumCards,
            Locations = locations,
            DateStart = dto.DateStart,
            DateEnd = dto.DateEnd,
            CommentFilter = dto.CommentFilter,
            Camera = dto.Camera,
            LocationId = dto.LocationId,
            SortBy = dto.SortBy,
            SortDirection = dto.SortDirection,
            North = dto.North,
            South = dto.South,
            West = dto.West,
            East = dto.East,
            EditLocation = dto.EditLocation,
            Cameras = cameras,
            // Placemarks = placemarks,
            SortByOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = SortBy.EarliestDate.ToString(), Text = "По дате" },
                    new SelectListItem { Value = SortBy.Name.ToString(), Text = "По названию" },
                    new SelectListItem { Value = SortBy.LeastLatitude.ToString(), Text = "По долготе" },
                    new SelectListItem { Value = SortBy.LeastLongitude.ToString(), Text = "По широте" },
                    new SelectListItem { Value = SortBy.ShotCount.ToString(), Text = "По размеру" }
                },
            SortDirectionOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = SortDirection.Descending.ToString(), Text = "По убыванию" },
                    new SelectListItem { Value = SortDirection.Ascending.ToString(), Text = "По возрастанию" }
                }
        };
    }

    public IQueryable<Shot> ApplyShotFilters(AlbumsListDTO dto, String username, bool onlyMine)
    {
        if (dto == null) return dbContext.Shots.AsNoTracking().AsQueryable();

        var provider = CultureInfo.InvariantCulture;
        var query = dbContext.Shots.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(dto.DateStart))
        {
            if (DateTime.TryParseExact(dto.DateStart, "yyyy", provider, DateTimeStyles.None, out var start))
            {
                query = query.Where(s => s.DateStart >= start);
            }
        }

        if (!string.IsNullOrEmpty(dto.DateEnd))
        {
            if (DateTime.TryParseExact(dto.DateEnd, "yyyy", provider, DateTimeStyles.None, out var end))
            {
                query = query.Where(s => s.DateEnd <= end);
            }
        }

        query = query.Where(s =>
            s.Latitude <= dto.North &&
            s.Latitude >= dto.South &&
            s.Longitude >= dto.West &&
            s.Longitude <= dto.East
        );

        if (!string.IsNullOrEmpty(dto.Camera))
        {
            query = query.Where(s => s.CameraModel == dto.Camera);
        }

        if (!string.IsNullOrEmpty(dto.CommentFilter))
        {
            var lowerFilter = dto.CommentFilter.ToLower();
            query = query.Where(s => dbContext.ShotComments
                .Any(c => c.ShotId == s.ShotId && c.Text.ToLower().Contains(lowerFilter)));
        }

        if (onlyMine)
        {
            query = query.Where(s => s.Album != null && s.Album.User != null && s.Album.User.Username == username);
        }
        else
        {
            // Get current user's ID
            var currentUser = dbContext.Users.FirstOrDefault(u => u.Username == username);
            if (currentUser == null)
            {
                return query.Where(s => false); // No user found, return empty
            }

            // Pre-fetch host user IDs that shared with current user (excluding disabled ones)
            var sharedHostIds = dbContext.SharedUsers
                .Where(su => su.GuestUserId == currentUser.UserId && !su.DisabledByGuest)
                .Select(su => su.HostUserId)
                .ToList();

            // Pre-fetch album IDs shared directly with current user
            var sharedAlbumIds = dbContext.SharedAlbums
                .Where(sa => sa.GuestUserId == currentUser.UserId)
                .Select(sa => sa.AlbumId)
                .ToList();

            query = query.Where(shot =>
                // Owned by current user
                (shot.Album != null && shot.Album.User != null && shot.Album.User.UserId == currentUser.UserId)
                // Or album belongs to a host who shared their library
                || (shot.Album != null && shot.Album.User != null && sharedHostIds.Contains(shot.Album.User.UserId))
                // Or album is directly shared
                || (shot.Album != null && sharedAlbumIds.Contains(shot.Album.AlbumId)));
        }

        return query;
    }

    public Album GetAlbum(int id)
    {
        return dbContext.Albums.Find(id);
    }

    public async Task DeleteAlbumAsync(int id)
    {
        // 1️⃣ Find album and its shots in one query (tracked)
        var album = await dbContext.Albums
            .Include(a => a.Shots)
            .FirstOrDefaultAsync(a => a.AlbumId == id);

        if (album == null)
            return;

        // 2️⃣ Collect file info before DB deletion
        var shotFiles = album.Shots
            .Select(s => new { s.SourceUri, s.Storage })
            .ToList();

        // 3️⃣ Start a transaction for atomic DB operations
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            // 4️⃣ Delete related shots first, then album
            dbContext.Shots.RemoveRange(album.Shots);
            dbContext.Albums.Remove(album);
            await dbContext.SaveChangesAsync();

            // 5️⃣ Commit the DB transaction — all DB work done successfully
            await transaction.CommitAsync();
        }
        catch
        {
            // Roll back if anything failed before committing
            await transaction.RollbackAsync();
            throw;
        }

        // 6️⃣ Now delete physical files (outside the transaction)
        // Doing this after commit avoids losing both DB + files if something goes wrong
        var deleteTasks = shotFiles
            .Select(s => Storage.DeleteFileAsync(s.Storage, s.SourceUri));

        // Parallel deletion (if Storage.DeleteFileAsync is thread-safe)
        await Task.WhenAll(deleteTasks);
    }


    public void UpdateAlbumName(int albumId, string name)
    {
        var album = dbContext.Albums.Find(albumId);
        if (album != null)
        {
            album.Name = name;
            dbContext.SaveChanges();
        }
    }

    public void UpdateAlbumPreview(int sourceAlbumId, List<int> movedShotIds, int targetAlbumId)
    {
        var sourceAlbum = dbContext.Albums.FirstOrDefault(a => a.AlbumId == sourceAlbumId);
        if (sourceAlbum == null) return;

        // Update preview of source album if its current preview was moved
        if (sourceAlbum.PreviewId.HasValue && movedShotIds.Contains(sourceAlbum.PreviewId.Value))
        {
            var newPreviewId = dbContext.Shots
                .Where(s => s.AlbumId == sourceAlbumId && !movedShotIds.Contains(s.ShotId))
                .Select(s => s.ShotId)
                .FirstOrDefault();

            sourceAlbum.PreviewId = newPreviewId != 0 ? newPreviewId : null;
        }

        // Ensure target album has a preview
        var targetAlbum = dbContext.Albums.FirstOrDefault(a => a.AlbumId == targetAlbumId);
        if (targetAlbum != null && (targetAlbum.PreviewId == 0 || targetAlbum.PreviewId == null) && movedShotIds.Any())
        {
            targetAlbum.PreviewId = movedShotIds[0];
        }

        dbContext.SaveChanges();
    }

    public List<Album> GetAlbums()
    {
        return dbContext.Albums.ToList();
    }

    public Album FindOrCreateAlbum(int userId, string albumName)
    {
        var user = dbContext.Users.Where(u => u.UserId == userId).FirstOrDefault();
        if (user == null) return null;

        var existingAlbum = dbContext.Albums
            .Where(a => a.User.UserId == userId && a.Name == albumName)
            .FirstOrDefault();

        if (existingAlbum != null)
        {
            return existingAlbum;
        }

        Album album = new Album
        {
            User = user,
            Name = albumName
        };
        dbContext.Add(album);
        dbContext.SaveChanges();

        return album;
    }

    public List<AlbumCardDTO> GetAlbumCardsForUser(string username, int excludeAlbumId)
    {
        return dbContext.Albums
            .Where(a => a.User != null && a.User.Username == username && a.AlbumId != excludeAlbumId)
            .GroupJoin(
                dbContext.Shots,
                album => album.PreviewId,
                shot => shot.ShotId,
                (album, shots) => new { Album = album, Shots = shots }
            )
            .SelectMany(
                x => x.Shots.DefaultIfEmpty(),
                (x, shot) => new AlbumCardDTO
                {
                    AlbumId = x.Album.AlbumId,
                    Name = x.Album.Name,
                    PreviewId = x.Album.PreviewId ?? 0,
                    PreviewFlip = shot != null ? shot.Flip : false,
                    PreviewRotate = shot != null ? shot.Rotate : 0
                }
            )
            .OrderBy(a => a.Name)
            .ToList();
    }

    public List<string> GetCameraModels()
    {
        return dbContext.Shots.Select(s => s.CameraModel).Distinct().ToList();
    }

    public Album GetAlbumWithUser(int id, int userId)
    {
        return dbContext.Albums
            .Include(a => a.User)
            .Include(a => a.AlbumComments)
            .FirstOrDefault(a => a.AlbumId == id && a.User.UserId == userId);
    }

    public void CreateAlbum(Album album)
    {
        dbContext.Albums.Add(album);
        dbContext.SaveChanges();
    }

    public Album GetAlbumByName(string name, int userId)
    {
        return dbContext.Albums
            .Include(a => a.User)
            .FirstOrDefault(a => a.Name == name && a.User.UserId == userId);
    }

    public (int? PrevId, int? NextId) GetAdjacentAlbumIds(int albumId)
    {
        var ownerUserId = dbContext.Albums
            .Where(a => a.AlbumId == albumId)
            .Select(a => a.User.UserId)
            .FirstOrDefault();

        if (ownerUserId == 0) return (null, null);

        var albums = dbContext.Albums
            .Where(a => a.User.UserId == ownerUserId)
            .OrderBy(a => a.Name)
            .Select(a => a.AlbumId)
            .ToList();

        var index = albums.IndexOf(albumId);
        if (index < 0) return (null, null);

        int? prevId = index > 0 ? albums[index - 1] : null;
        int? nextId = index < albums.Count - 1 ? albums[index + 1] : null;
        return (prevId, nextId);
    }

}