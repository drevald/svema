using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System;
using Svema.Controllers;
using Data;
using Form;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace Test
{
    public class RestControllerTests
    {
        private static ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;

            var dbContext = new ApplicationDbContext(options);

            // Seed initial data for testing
            User user = new() { UserId = 1, Username = "John Doe" };
            ShotStorage storage = new() { Id = 1, UserId = 1 };
            dbContext.Users.Add(user);
            dbContext.Albums.Add(new Album { AlbumId = 1, Name = "Holiday Photos", User = user });
            dbContext.Shots.Add(new Shot { ShotId = 1, Name = "Shot1", AlbumId = 1, Storage = storage });
            dbContext.SaveChanges();
            return dbContext;
        }

        [Fact]
        public void GetAlbums_ReturnsAllAlbums()
        {
            Console.Write(">>>>> 1 started");
            // Arrange
            var dbContext = CreateInMemoryContext();
            var configuration = new ConfigurationBuilder().Build();
            var controller = new RestController(dbContext, configuration);

            // Act
            var result = controller.GetAlbums();

            // Assert
            var albums = Assert.IsType<JsonResult>(result).Value as IEnumerable<Album>;
            Assert.NotNull(albums);
            Assert.Single(albums);
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
            Console.Write("<<<<< 1 ended");
        }

        [Fact]
        public void GetAlbum_ReturnsSpecificAlbum()
        {
            Console.Write(">>>>> 2 started");

            // Arrange
            var dbContext = CreateInMemoryContext();
            var configuration = new ConfigurationBuilder().Build();
            var controller = new RestController(dbContext, configuration);

            // Act
            var result = controller.GetAlbum(1);

            // Assert
            var album = Assert.IsType<JsonResult>(result).Value as Album;
            Assert.NotNull(album);
            Assert.Equal("Holiday Photos", album.Name);
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();

            Console.Write("<<<<< 2 ended");
        }

        [Fact]
        public void PostAlbum_AddsNewAlbum()
        {
            Console.Write(">>>>> 3 started");

            // Arrange
            var dbContext = CreateInMemoryContext();
            var configuration = new ConfigurationBuilder().Build();
            var controller = new RestController(dbContext, configuration);

            var newAlbum = new AlbumDTO { UserId = 1, Name = "New Album" };

            // Act
            var result = controller.PostAlbum(newAlbum);

            // Assert
            var album = Assert.IsType<JsonResult>(result).Value as Album;
            Assert.NotNull(album);
            Assert.Equal("New Album", album.Name);

            // Verify the album is in the database
            Assert.Equal(2, dbContext.Albums.Count());
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();

            Console.Write("<<<<< 3 ended");
        }

        [Fact]
        public async Task PostShot_AddsNewShot()
        {
            Console.Write(">>>>> 4 started");

            // Arrange
            var dbContext = CreateInMemoryContext();
            var configuration = new ConfigurationBuilder().Build();
            var controller = new RestController(dbContext, configuration);

            var newShot = new ShotREST
            {
                UserId = 1,
                AlbumId = 1,
                Name = "New Shot",
                DateStart = DateTime.Now,
                DateEnd = DateTime.Now.AddHours(1),
                Data = File.ReadAllBytes("Resources\\DSC02678.png"),
                Mime = "image/jpeg"
            };

            // Act
            await controller.PostShot(newShot);

            // Assert
            Assert.Equal(2, dbContext.Shots.Count());
            var shot = dbContext.Shots.Last();
            Assert.Equal("New Shot", shot.Name);
            dbContext.Dispose();

            Console.Write("<<<<< 4 ended");
        }
    }
}