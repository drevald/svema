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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Utils;

namespace Services;

public class FileService : Service
{

    public FileService(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public PhotoMetadata GetMetadata(byte[] data, string name, Dictionary<string, string> fileErrors)
    {
        try
        {
            return ImageUtils.GetMetadata(data);
        }
        catch (Exception e)
        {
            Console.WriteLine("GetMetadata Exception: " + e.Message);
            fileErrors.Add(name, e.Message);
            return null;
        }
    }

    public async Task ProcessShot(byte[] data, string name, string mime, Shot shot, Album album, ShotStorage storage, Dictionary<string, string> fileErrors, PhotoMetadata photoMetadata)
    {
        try
        {
            using var md5 = MD5.Create();
            using var originalStream = new MemoryStream(data);

            // 1. Compute MD5 first (Optimization: Check for duplicates before heavy processing)
            string md5Hash = BitConverter.ToString(md5.ComputeHash(originalStream)).Replace("-", "").ToLowerInvariant();

            // 2. Check for duplicates in DB
            if (await dbContext.Shots.AnyAsync(s => s.MD5 == md5Hash))
            {
                Console.WriteLine($"[DEBUG] Duplicate file detected: {name} ({md5Hash})");
                fileErrors.Add(name, "Same file already stored");
                return;
            }

            shot.MD5 = md5Hash;

            // 3. Proceed with heavy image processing
            originalStream.Position = 0; // Reset stream position
            using var previewStream = new MemoryStream(data);
            using var fullStream = new MemoryStream(data);
            using var previewImage = Image.Load(previewStream);
            using var fullImage = Image.Load(fullStream);

            // Preview
            shot.Preview = GetImagePreview(previewImage);

            // Fullscreen
            shot.FullScreen = GetFullSizeImage(fullImage);

            // Metadata
            if (photoMetadata.CreationDate != null)
            {
                shot.DateStart = photoMetadata.CreationDate!.Value;
                shot.DateEnd = photoMetadata.CreationDate!.Value;
            }
            shot.Size = data.Length;
            shot.ContentType = mime;
            shot.Name = name;
            shot.Album = album;
            shot.Storage = storage;
            shot.CameraManufacturer = photoMetadata.CameraManufacturer;
            shot.CameraModel = photoMetadata.CameraModel;

            if (photoMetadata.Latitude != null && photoMetadata.Longitude != null)
            {
                shot.Latitude = (float)photoMetadata.Latitude;
                shot.Longitude = (float)photoMetadata.Longitude;
                shot.Zoom = 15;
            }

            Console.WriteLine($"[DEBUG] Before AddShotToDatabase. album is null: {album == null}, shot is null: {shot == null}");
            if (album != null)
            {
                Console.WriteLine($"[DEBUG] Album details: AlbumId={album.AlbumId}, Name={album.Name}, User is null: {album.User == null}");
            }

            AddShotToDatabase(shot, album);

            await Storage.StoreShot(shot, originalStream.ToArray());

            Console.WriteLine(name, "File successfully added");
        }
        catch (DbUpdateException e)
        {
            dbContext.Entry(shot).State = EntityState.Detached;
            Console.WriteLine("The DbUpdateException is: " + e.Message);
            fileErrors.Add(name, "Same file already stored (race condition)");
        }
        catch (Exception e)
        {
            Console.WriteLine($"ProcessShot Exception: {e.Message}");
            Console.WriteLine($"Stack Trace: {e.StackTrace}");
            fileErrors.Add(name, e.Message);
        }
    }

    private void ProcessImage(Image image)
    {
        image.Mutate(x => x.AutoOrient());

        float ratio = (float)image.Width / image.Height;

        // Resize for preview (200px max)
        if (ratio > 1)
        {
            image.Mutate(x => x.Resize((int)(200 * ratio), 200));
            image.Mutate(x => x.Crop(new Rectangle((image.Width - 200) / 2, 0, 200, 200)));
        }
        else
        {
            image.Mutate(x => x.Resize(200, (int)(200 / ratio)));
            image.Mutate(x => x.Crop(new Rectangle(0, (image.Height - 200) / 2, 200, 200)));
        }
    }

    private byte[] GetImagePreview(Image image)
    {
        using var outputStream = new MemoryStream();
        ProcessImage(image);
        ImageExtensions.SaveAsJpeg(image, outputStream);
        return outputStream.GetBuffer();
    }

    private byte[] GetFullSizeImage(Image image)
    {
        // Normalize EXIF orientation into pixels so stored image has no EXIF rotation
        image.Mutate(x => x.AutoOrient());

        // Define max width and height
        int maxWidth = 1920;
        int maxHeight = 1080;

        // Calculate the scaling factors for width and height
        float widthRatio = (float)maxWidth / image.Width;
        float heightRatio = (float)maxHeight / image.Height;

        // Choose the smaller ratio to maintain the aspect ratio
        float scaleRatio = Math.Min(widthRatio, heightRatio);

        // Calculate the new dimensions
        int newWidth = (int)(image.Width * scaleRatio);
        int newHeight = (int)(image.Height * scaleRatio);

        // Resize the image
        image.Mutate(x => x.Resize(newWidth, newHeight));

        using var outputStream = new MemoryStream();
        ImageExtensions.SaveAsJpeg(image, outputStream);
        return outputStream.GetBuffer();
    }

    private void AddShotToDatabase(Shot shot, Album album)
    {
        dbContext.Shots.Add(shot);
        dbContext.SaveChanges();
        if (album.PreviewId == null || album.PreviewId == 0)
        {
            album.PreviewId = shot.ShotId;
            dbContext.Albums.Update(album);
        }
        shot.SourceUri = $"user_{album.User.UserId}/album_{album.AlbumId}/shot_{shot.ShotId}";
        dbContext.SaveChanges();
    }

}
