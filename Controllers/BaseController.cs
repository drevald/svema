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

namespace Controllers;

public class BaseController: Controller {

    protected ApplicationDbContext dbContext;

    protected IConfiguration config;

    public BaseController (ApplicationDbContext dbContext, IConfiguration config) {
        this.dbContext = dbContext;
        this.config = config;
    }

    public async Task<Dictionary<string, string>> ProcessShot(byte[] data, string name, string mime, Shot shot, Album album, ShotStorage storage, Dictionary<string, string> errors) {
        try {
            using var md5 = MD5.Create();
            using var stream = new MemoryStream(data);
            using var stream1 = new MemoryStream(data);
            using var outputStream = new MemoryStream();
            stream.Position = 0;
            stream1.Position = 0;
            using var image = Image.Load(stream);
            float ratio = (float)image.Width/(float)image.Height;
            if (ratio > 1 ) {
                image.Mutate(x => x.Resize((int)(200 * ratio), 200));
                image.Mutate(x => x.Crop(new Rectangle((image.Width-200)/2, 0, 200, 200)));
            } else {
                image.Mutate(x => x.Resize(200, (int)(200 / ratio)));
                image.Mutate(x => x.Crop(new Rectangle(0, (image.Height-200)/2, 200, 200)));
            }
            ImageExtensions.SaveAsJpeg(image, outputStream);
            shot.Size = data.Length;
            shot.ContentType = mime;
            shot.Name = name;
            shot.Album = album;
            shot.Preview = outputStream.GetBuffer();
            shot.Storage = storage;
            stream.Position = 0;
            shot.MD5 = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            dbContext.Shots.Add(shot);
            await dbContext.SaveChangesAsync();
            if (album.PreviewId == 0) {
                album.PreviewId = shot.ShotId;
                dbContext.Albums.Update(album);
            }
            shot.SourceUri = "user_" + album.User.UserId + "/album_" + album.AlbumId + "/shot_" + shot.ShotId;
            Storage.StoreShot(shot, stream1.ToArray());
            await dbContext.SaveChangesAsync();
        }   catch (DbUpdateException e) {
            System.Console.Write("The DbUpdateException is " + e.Data);
            errors.Add(name, e.InnerException.Message);
        }   catch (Exception e) {
            System.Console.Write("The Exception is " + e.Data);
            errors.Add(name, e.Message);
        } 
        return errors;
    }

}