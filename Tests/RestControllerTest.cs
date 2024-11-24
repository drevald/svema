using Moq;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using Controllers;
using Data;
using Form;
using Microsoft.Extensions.Configuration;

public class RestControllerTests
{
    private readonly Mock<ApplicationDbContext> _mockDbContext;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly RestController _controller;

    public RestControllerTests()
    {
        _mockDbContext = new Mock<ApplicationDbContext>();
        _mockConfig = new Mock<IConfiguration>();
        
        // Setup any necessary mocks for DbSet
        var mockAlbums = new Mock<DbSet<Album>>();
        var mockUsers = new Mock<DbSet<User>>();
        var mockShotStorages = new Mock<DbSet<ShotStorage>>();
        var mockShots = new Mock<DbSet<Shot>>();
        
        _mockDbContext.Setup(db => db.Albums).Returns(mockAlbums.Object);
        _mockDbContext.Setup(db => db.Users).Returns(mockUsers.Object);
        _mockDbContext.Setup(db => db.ShotStorages).Returns(mockShotStorages.Object);
        _mockDbContext.Setup(db => db.Shots).Returns(mockShots.Object);

        _controller = new RestController(_mockDbContext.Object, _mockConfig.Object);
    }

    [Fact]
    public void GetAlbums_ReturnsJsonResult()
    {
        // Arrange
        var mockAlbums = new List<Album> { new Album { Name = "Album 1" }, new Album { Name = "Album 2" } }.AsQueryable();
        var mockDbSet = new Mock<DbSet<Album>>();
        mockDbSet.As<IQueryable<Album>>().Setup(m => m.Provider).Returns(mockAlbums.Provider);
        mockDbSet.As<IQueryable<Album>>().Setup(m => m.Expression).Returns(mockAlbums.Expression);
        mockDbSet.As<IQueryable<Album>>().Setup(m => m.ElementType).Returns(mockAlbums.ElementType);
        mockDbSet.As<IQueryable<Album>>().Setup(m => m.GetEnumerator()).Returns(mockAlbums.GetEnumerator());

        _mockDbContext.Setup(db => db.Albums).Returns(mockDbSet.Object);

        // Act
        var result = _controller.GetAlbums();

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var albums = Assert.IsAssignableFrom<IEnumerable<Album>>(jsonResult.Value);
        Assert.Equal(2, albums.Count());
    }

    [Fact]
    public async Task PostShot_CreatesShotAndProcessesIt()
    {
        // Arrange
        var dto = new ShotREST
        {
            AlbumId = 1,
            UserId = 1,
            DateStart = DateTime.Now,
            DateEnd = DateTime.Now.AddHours(1),
            Data = new byte[] { 1, 2, 3 },
            Name = "Shot1",
            Mime = "image/jpeg"
        };

        var mockAlbum = new Album { AlbumId = 1 };
        var mockUser = new User { UserId = 1 };
        var mockStorage = new ShotStorage { User = mockUser };

        _mockDbContext.Setup(db => db.Albums.Find(1)).Returns(mockAlbum);
        _mockDbContext.Setup(db => db.Users.Where(u => u.UserId == 1).First()).Returns(mockUser);
        _mockDbContext.Setup(db => db.ShotStorages.Where(s => s.User == mockUser).First()).Returns(mockStorage);

        var mockShot = new Mock<Shot>();
        _mockDbContext.Setup(db => db.Shots.Add(It.IsAny<Shot>()));

        // Act
        await _controller.PostShot(dto);

        // Assert
        _mockDbContext.Verify(db => db.Shots.Add(It.IsAny<Shot>()), Times.Once);
        _mockDbContext.Verify(db => db.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task PostAlbum_ReturnsCreatedAlbum()
    {
        // Arrange
        var dto = new AlbumDTO { UserId = 1, Name = "New Album" };
        var mockUser = new User { UserId = 1 };

        _mockDbContext.Setup(db => db.Users.Where(u => u.UserId == 1).First()).Returns(mockUser);
        
        var mockAlbum = new Album { User = mockUser, Name = "New Album" };
        _mockDbContext.Setup(db => db.Add(It.IsAny<Album>()));
        _mockDbContext.Setup(db => db.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = _controller.PostAlbum(dto);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var album = Assert.IsType<Album>(jsonResult.Value);
        Assert.Equal("New Album", album.Name);
    }
}
