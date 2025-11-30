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
using Services;

namespace Svema.Controllers;

public class BaseController : Controller
{

    public LocationService locationService;
    public AlbumService albumService;
    public CommentService commentService;
    public ShotService shotService;
    public UserService userService;
    public FileService fileService;

    protected ApplicationDbContext dbContext;

    protected IConfiguration config;

    public BaseController(ApplicationDbContext dbContext, IConfiguration config)
    {
        this.dbContext = dbContext;
        this.config = config;
        this.locationService = new LocationService(dbContext);
        this.albumService = new AlbumService(dbContext);
        this.commentService = new CommentService(dbContext);
        this.shotService = new ShotService(dbContext);
        this.userService = new UserService(dbContext);
        this.fileService = new FileService(dbContext);
    }


    public async Task ProcessShot(byte[] data, string name, string mime, Shot shot, Album album, ShotStorage storage, Dictionary<string, string> fileErrors, PhotoMetadata photoMetadata)
    {
        await fileService.ProcessShot(data, name, mime, shot, album, storage, fileErrors, photoMetadata);
    }

    protected string GetUsername()
    {
        return HttpContext.User?.FindFirst(ClaimTypes.Name)?.Value;
        //return HttpContext?.User?.FindFirst("user")?.Value ?? string.Empty;
    }

    protected int? GetUserId()
    {
        var idValue = HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(idValue, out var id))
            return id;

        return null; // or 0 if you prefer
    }

}
