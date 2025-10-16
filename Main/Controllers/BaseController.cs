using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Data;
using Utils;
using System.Security.Claims;

namespace Controllers;

public class BaseController : Controller
{

    protected ApplicationDbContext dbContext;

    protected IConfiguration config;

    public BaseController(ApplicationDbContext dbContext, IConfiguration config)
    {
        this.dbContext = dbContext;
        this.config = config;
    }


    public async Task ProcessShot(byte[] data, string name, string mime, Shot shot, Album album, ShotStorage storage, Dictionary<string, string> fileErrors)
    {
        try
        {
            using var md5 = MD5.Create();
            using var originalStream = new MemoryStream(data);
            using var previewStream = new MemoryStream(data);
            using var fullStream = new MemoryStream(data);
            using var previewImage = Image.Load(previewStream);
            using var fullImage = Image.Load(fullStream);

            PhotoMetadata photoMetadata = ImageUtils.GetMetadata(data);

            // Preview
            shot.Preview = GetImagePreview(previewImage);

            // Fullscreen
            shot.FullScreen = GetFullSizeImage(fullImage);

            // Metadata
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

            originalStream.Position = 0;
            shot.MD5 = BitConverter.ToString(md5.ComputeHash(originalStream)).Replace("-", "").ToLowerInvariant();
            AddShotToDatabase(shot, album);

            await Storage.StoreShot(shot, originalStream.ToArray());

            fileErrors.Add(name, "File successfully added");
        }
        catch (DbUpdateException e)
        {
            dbContext.Entry(shot).State = EntityState.Detached;
            Console.WriteLine("The DbUpdateException is " + e.Data);
            fileErrors.Add(name, "Same file already stored");
        }
        catch (Exception e)
        {
            Console.WriteLine("The Exception is " + e.Data);
            fileErrors.Add(name, e.Message);
        }
    }


    private void ProcessImage(Image image)
    {
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

    private string CalculateMD5(MemoryStream stream, MD5 md5)
    {
        stream.Position = 0;
        return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private void AddShotToDatabase(Shot shot, Album album)
    {
        dbContext.Shots.Add(shot);
        dbContext.SaveChanges();
        if (album.PreviewId == 0)
        {
            album.PreviewId = shot.ShotId;
            dbContext.Albums.Update(album);
        }
        shot.SourceUri = $"user_{album.User.UserId}/album_{album.AlbumId}/shot_{shot.ShotId}";
        dbContext.SaveChanges();
    }

    private async Task StoreShotInStorage(Shot shot, Album album, MemoryStream stream)
    {
        await Storage.StoreShot(shot, stream.ToArray());
    }

    private void HandleDbUpdateException(DbUpdateException e, Shot shot, Dictionary<string, string> fileErrors, string name)
    {
        dbContext.Entry(shot).State = EntityState.Detached;
        System.Console.WriteLine("The DbUpdateException is " + e.Data);
        fileErrors.Add(name, "Same file already stored");
    }

    private void HandleException(Exception e, Dictionary<string, string> fileErrors, string name)
    {
        System.Console.WriteLine("The Exception is " + e.Data);
        fileErrors.Add(name, e.Message);
    }

    protected string GetUsername()
    {
        return HttpContext?.User?.FindFirst("user")?.Value ?? string.Empty;
    }

    protected int? GetUserId()
    {
        var idValue = HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(idValue, out var id))
            return id;

        return null; // or 0 if you prefer
    }

}