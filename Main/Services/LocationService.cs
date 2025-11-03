using Form;
using Npgsql;
using Data;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq;
using System;

namespace Services;

public class LocationService : Service
{

    public LocationService(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

//To get clustered view of locations on map
    public List<LocationDTO> GetClusteredShotsWithLabels(String username, bool onlyMine, double longitudeMin, double longitudeMax, double latitudeMin, double latitudeMax)
    {
        var filters = new List<string>();
        filters.Add("s.longitude BETWEEN @longitudeMin AND @longitudeMax");
        filters.Add("s.latitude BETWEEN @latitudeMin AND @latitudeMax");

        if (onlyMine)
        {
            filters.Add("u.username = @username");
        }
        else
        {
            filters.Add(@"(
                u.username = @username
                OR EXISTS (
                    SELECT 1 FROM shared_users su
                    WHERE su.guest_user_id = (SELECT ""UserId"" FROM users WHERE username = @username)
                    AND su.host_user_id = a.""UserId""
                )
                OR EXISTS (
                    SELECT 1 FROM shared_albums sa
                    JOIN albums shared_a ON sa.shared_album_id = shared_a.id
                    WHERE sa.guest_user_id = (SELECT ""UserId"" FROM users WHERE username = @username)
                    AND shared_a.id = a.id
                )
            )");
        }

        string whereClause = string.Join(" AND ", filters);

        string sql = $@"
        SELECT 
            COUNT(*) AS count,
            ST_X(ST_Centroid(ST_Collect(geom))) AS lon,
            ST_Y(ST_Centroid(ST_Collect(geom))) AS lat
        FROM (
            SELECT ST_SnapToGrid(
                    ST_SetSRID(ST_MakePoint(s.longitude, s.latitude), 4326), @gridSize
                ) AS tile_geom,
                ST_SetSRID(ST_MakePoint(s.longitude, s.latitude), 4326) AS geom
            FROM shots s
            JOIN albums a ON s.album_id = a.id
            JOIN users u ON a.""UserId"" = u.id
            WHERE {whereClause}
        ) AS clustered
        GROUP BY tile_geom;
        ";

        var locationList = new List<LocationDTO>();

        using (var connection = dbContext.Database.GetDbConnection())
        {
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                command.Parameters.Add(new NpgsqlParameter("@gridSize", (longitudeMax - longitudeMin) / 100));
                command.Parameters.Add(new NpgsqlParameter("@longitudeMin", longitudeMin));
                command.Parameters.Add(new NpgsqlParameter("@longitudeMax", longitudeMax));
                command.Parameters.Add(new NpgsqlParameter("@latitudeMin", latitudeMin));
                command.Parameters.Add(new NpgsqlParameter("@latitudeMax", latitudeMax));
                command.Parameters.Add(new NpgsqlParameter("@username", username));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var location = new LocationDTO
                        {
                            Label = reader.GetInt32(reader.GetOrdinal("count")).ToString(),
                            Longitude = reader.GetDouble(reader.GetOrdinal("lon")),
                            Latitude = reader.GetDouble(reader.GetOrdinal("lat"))
                        };
                        locationList.Add(location);
                    }
                }
            }
        }
        return locationList;
    }

    public List<LocationDTO> GetShotsForShots(List<ShotPreviewDTO> shots)
    {
        if (shots == null || shots.Count == 0)
            return new List<LocationDTO>();

        var ids = shots.Select(s => s.ShotId).ToList();
        if (ids.Count == 0)
            return new List<LocationDTO>();

        string idParams = string.Join(",", ids);

        string sql = $@"
        SELECT 
            COUNT(*) AS count,
            s.longitude AS lon,
            s.latitude AS lat
        FROM shots s
        WHERE s.id IN ({idParams})
        GROUP BY s.longitude, s.latitude;
    ";

        var locations = new List<LocationDTO>();

        // get the underlying ADO.NET connection but don’t dispose dbContext
        var conn = dbContext.Database.GetDbConnection();
        bool wasOpen = conn.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    locations.Add(new LocationDTO
                    {
                        Label = reader.GetInt32(reader.GetOrdinal("count")).ToString(),
                        Longitude = reader.GetDouble(reader.GetOrdinal("lon")),
                        Latitude = reader.GetDouble(reader.GetOrdinal("lat"))
                    });
                }
            }
        }

        if (!wasOpen)
            conn.Close();

        return locations;
    }


}

