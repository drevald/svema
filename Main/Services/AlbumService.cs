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
        return dbContext.Albums
        .Include(a => a.AlbumComments)
        .FirstOrDefault(a =>
            a.AlbumId == id &&
            (
                // 1️⃣ Owned by current user
                a.User.UserId == currentUserId ||

                // 2️⃣ Shared directly to this user
                dbContext.SharedAlbums.Any(sa =>
                    sa.AlbumId == a.AlbumId &&
                    sa.GuestUserId == currentUserId
                ) ||

                // 3️⃣ Shared via host-user relationship (shared user)
                dbContext.SharedUsers.Any(su =>
                    su.HostUserId == a.User.UserId &&
                    su.GuestUserId == currentUserId
                ) ||

                // 4️⃣ Public shared link (still active)
                dbContext.SharedLinks.Any(sl =>
                    sl.ResourceType == "album" &&
                    sl.ResourceId == a.AlbumId &&
                    !sl.Revoked &&
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
                PreviewId = a.PreviewId,
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
            // North = rect.North,
            // South = rect.South,
            // West = rect.West,
            // East = rect.East,
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

        if (onlyMine)
        {
            query = query.Where(s => s.Album != null && s.Album.User != null && s.Album.User.Username == username);
        }
        else
        {
            query = query.Where(shot =>
                    (shot.Album != null && shot.Album.User != null && shot.Album.User.Username == username)
                    || dbContext.SharedUsers.Any(su =>
                        su.GuestUser != null && su.GuestUser.Username == username &&
                        su.HostUser != null && su.HostUser.UserId == (shot.Album != null && shot.Album.User != null ? shot.Album.User.UserId : -1))
                    || dbContext.SharedAlbums.Any(sa =>
                        sa.GuestUser != null && sa.GuestUser.Username == username &&
                        sa.Album != null && sa.Album.AlbumId == (shot.Album != null ? shot.Album.AlbumId : -1)));
        }


        return query;
    }

    public Album GetAlbum(int id)
    {
        return dbContext.Albums.Find(id);
    }

    public Album GetAlbumWithUser(int id, int currentUserId)
    {
        return dbContext.Albums
            .Include(a => a.AlbumComments)
            .FirstOrDefault(a => a.AlbumId == id && a.User.UserId == currentUserId);
    }

    public void CreateAlbum(Album album)
    {
        dbContext.Add(album);
        dbContext.SaveChanges();
    }

    public async Task DeleteAlbum(int id)
    {
        var album = dbContext.Albums.Find(id);
        if (album == null) return;

        var shotInfos = dbContext.Shots
            .Where(s => s.AlbumId == id)
            .Select(s => new
            {
                s.SourceUri,
                s.Storage
            })
            .ToList();

        foreach (var shot in shotInfos)
        {
            await Storage.DeleteFileAsync(shot.Storage, shot.SourceUri);
        }

        dbContext.Shots.RemoveRange(dbContext.Shots.Where(s => s.AlbumId == id));
        dbContext.Albums.Remove(album);
        dbContext.SaveChanges();
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
        if (movedShotIds.Contains(sourceAlbum.PreviewId))
        {
            var newPreviewId = dbContext.Shots
                .Where(s => s.AlbumId == sourceAlbumId && !movedShotIds.Contains(s.ShotId))
                .Select(s => s.ShotId)
                .FirstOrDefault();

            sourceAlbum.PreviewId = newPreviewId != 0 ? newPreviewId : 0;
        }

        // Ensure target album has a preview
        var targetAlbum = dbContext.Albums.FirstOrDefault(a => a.AlbumId == targetAlbumId);
        if (targetAlbum != null && targetAlbum.PreviewId == 0 && movedShotIds.Any())
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
                    PreviewId = x.Album.PreviewId,
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

}