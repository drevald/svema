using Form;
using Npgsql;
using Data;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq;

namespace Services;

public class LocationService : Service
{

    public LocationService(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public List<LocationDTO> GetShotsForShots(List<ShotPreviewDTO> shots)
{
    if (shots == null || shots.Count == 0)
        return new List<LocationDTO>();

    // Prepare WHERE clause for the selected shots by their IDs
    var idParams = string.Join(",", shots.Select((s, i) => s.ShotId));

    string sql = $@"
        SELECT 
            COUNT(*) AS count,
            s.longitude AS lon,
            s.latitude AS lat
        FROM shots s
        WHERE s.id IN ({idParams})
        GROUP BY s.longitude, s.latitude;
    ";

    var locationList = new List<LocationDTO>();

    using (var connection = dbContext.Database.GetDbConnection())
    {
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    locationList.Add(new LocationDTO
                    {
                        Label = reader.GetInt32(reader.GetOrdinal("count")).ToString(),
                        Longitude = reader.GetDouble(reader.GetOrdinal("lon")),
                        Latitude = reader.GetDouble(reader.GetOrdinal("lat"))
                    });
                }
            }
        }
    }

    return locationList;
}


}

