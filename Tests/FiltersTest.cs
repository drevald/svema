using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Form;
using Data;
using Svema.Controllers;
using Microsoft.Extensions.Configuration;       // For ConfigurationBuilder
using Microsoft.AspNetCore.Mvc;                // For ControllerContext
using System.Security.Claims;                  // For ClaimsPrincipal & Claim


namespace Test
{

    public class FiltersTest
    {

        [Fact]
        public void ApplyShotFilters_FiltersByDateRange()
        {
            // Arrange: Prepare in-memory DB
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new ApplicationDbContext(options);

            var user = new User { Username = "denis", UserId = 1 };

            context.Shots.AddRange(
                new Shot
                {
                    ShotId = 1,
                    DateStart = new DateTime(2020, 1, 1),
                    DateEnd = new DateTime(2020, 12, 31),
                    Latitude = 0,
                    Longitude = 0,
                    Album = new Album { User = user, Name = "Album" }
                },
                new Shot
                {
                    ShotId = 2,
                    DateStart = new DateTime(2019, 1, 1),
                    DateEnd = new DateTime(2019, 12, 31),
                    Latitude = 0,
                    Longitude = 0,
                    Album = new Album { User = user, Name = "Album" }
                });

            context.SaveChanges();

            // Mock IConfiguration if needed (you can leave it empty if not used in test)
            var inMemorySettings = new Dictionary<string, string>();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            // Create controller and inject context + config
            var controller = new MainController(context, config)
            {
                // Inject fake HttpContext with user identity
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim("user", "denis")
                        ]))
                    }
                }
            };

            // Prepare input DTO
            var dto = new AlbumsListDTO
            {
                DateStart = "2020",
                DateEnd = "2022",
                North = 90,
                South = -90,
                East = 180,
                West = -180
            };

            // Act
            var result = controller.ApplyShotFilters(dto, onlyMine: true).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(1, result[0].ShotId);
        }

    }

}
