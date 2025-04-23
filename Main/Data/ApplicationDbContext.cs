using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Data;

public class ApplicationDbContext : DbContext {

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base (options) {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public void AddOrUpdateEntity<TEntity>(TEntity entity) where TEntity : class {
        var entry = Entry(entity);
        switch (entry.State) {
            case EntityState.Detached:
                Add(entity);
                break;
            case EntityState.Modified:
                Update(entity);
                break;
            case EntityState.Unchanged:
                // If the entity is already in the database, but not tracked by the context,
                // re-attaching it will mark it as modified when SaveChanges is called.
                Attach(entity);
                Entry(entity).State = EntityState.Modified;
                break;
            // Add additional cases as needed
        }
    }

    protected override void OnModelCreating(ModelBuilder builder) {
        builder.Entity<Shot>(entity => {
            entity.HasIndex(e => e.MD5).IsUnique(true);
        });
        builder.Entity<ShotStorage>()
        .Property(e => e.Provider)
        .HasConversion<string>();
    }

    public DbSet<Album> Albums {get; set;}
    public DbSet<Shot> Shots {get; set;}
    public DbSet<Location> Locations {get; set;}
    public DbSet<Person> Persons {get; set;}
    public DbSet<User> Users {get; set;}
    public DbSet<AlbumComment> AlbumComments {get; set;}
    public DbSet<ShotComment> ShotComments {get; set;}
    public DbSet<ShotStorage> ShotStorages {get; set;}
    public DbSet<SharedUser> SharedUsers {get; set;}
    public DbSet<SharedAlbum> SharedAlbums {get; set;}

}

[Table("albums")]
public class Album {
    [Column("id")]
    public int AlbumId {get; set;}
    [Column("name")]
    public string Name {get; set;}
    [Column("user_id")]
    public User User {get; set;}
    [Column("preview_id")]
    public int PreviewId {get; set;}
    [JsonIgnore]
    public ICollection<Shot> Shots {get;}
    public ICollection<AlbumComment> AlbumComments {get; set;}
    [Column("longitude")]
    public double Longitude {get; set;}
    [Column("latitude")]
    public double Latitude {get; set;}
    [Column("zoom")]
    public int Zoom {get; set;}
}

[Table("shots")]
public class Shot {
    [Column("id")]
    public int ShotId {get; set;}
    [Column("name")]
    public string Name {get; set;}
    public Album Album {get; set;}    
    [Column("album_id")]
    public int AlbumId {get; set;}
    [BindProperty, DataType(DataType.Date), Column("date_start")]
    public DateTime DateStart {get; set;}
    [BindProperty, DataType(DataType.Date), Column("date_end")] 
    public DateTime DateEnd {get; set;}
    [BindProperty, DataType(DataType.Date), Column("date_uploaded")] 
    public DateTime DateUploaded {get; set;}
    [Column("preview")]
    public byte[] Preview {get; set;}
    [Column("fullscreen")]
    public byte[] FullScreen {get; set;}
    [Column("source_uri")]
    public string SourceUri {get; set;}
    [Column("orig_path")]
    public string OrigPath {get; set;}
    public string MD5 {get; set;}
    public ICollection<Person> Persons {get; set;}
    [Column("content_type")]
    public string ContentType {get; set;}
    public ICollection<ShotComment> ShotComments {get; set;}        
    [Column("storage_id")]
    public int StorageId {get; set;}
    public ShotStorage Storage {get; set;}
    [Column("size")]
    public long Size {get;set;}
    [Column("longitude")]
    public double Longitude {get; set;}
    [Column("latitude")]
    public double Latitude {get; set;}
    [Column("zoom")]
    public int Zoom {get; set;}
    [Column("rotate")]
    public int Rotate {get; set;}
    [Column("flip")]
    public bool Flip {get; set;}
    [Column("direction")]
    public float Direction {get; set;}
    [Column("angle")]
    public float Angle {get; set;}    
    [Column("camera_manufacturer")]
    public string CameraManufacturer {get; set;}
    [Column("camera_model")]
    public string CameraModel {get; set;}
    
}

[Table("locations")]
public class Location {
    [Column("id")]
    public int Id {get; set;}
    [Column("name")]
    public string Name {get; set;}
    [Column("longitude")]
    public double Longitude {get; set;}
    [Column("latitude")]
    public double Latitude {get; set;}
    [Column("zoom")]
    public int Zoom {get; set;}
}

[Table("persons")]
public class Person {
    [Column("id")]
    public int PersonId {get; set;}
    [Column("first_name")]
    public string FirstName {get; set;}
    [Column("last_name")]
    public string LastName {get; set;} 
    public ICollection<Shot> Shots {get; set;}
}

[Table("users")]
public class User {
    [Column("id")]
    public int UserId {get; set;}
    [Column("username")]
    public string Username {get; set;}
    [Column("password_hash")]
    public string PasswordHash {get; set;}
    [Column("email")]
    public string Email {get; set;}
}

[Table("shot_comments")]
public class ShotComment {
    [Column("id")]
    public int Id {get; set;}
    public User Author {get; set;}
    [Column("author_id")]
    public int AuthorId {get; set;}
    [Column("author_username")]
    public string AuthorUsername {get; set;}
    [Column("shot_id")]
    public int ShotId {get; set;}
    public Shot Shot {get; set;}
    [Column("time")]
    public DateTime Timestamp {get; set;}
    [Column("text")]
    public string Text {get; set;}
}

[Table("album_comments")]
public class AlbumComment {
    [Column("id")]
    public int Id {get; set;}
    public User Author {get; set;}
    [Column("author_id")]
    public int AuthorId {get; set;}
    [Column("author_username")]
    public string AuthorUsername {get; set;}
    public Album Album {get; set;}
    [Column("album_id")]
    public int AlbumId {get; set;}
    [Column("time")]
    public DateTime Timestamp {get; set;}
    [Column("text")]
    public string Text {get; set;}
}

[Table("shared_users")]
public class SharedUser {
    [Column("id")]
    public int Id {get; set;}
    [Column("host_user_id")]
    public int HostUserId {get; set;}
    public User HostUser {get; set;}
    [Column("guest_user_id")]
    public int GuestUserId {get; set;}
    public User GuestUser {get; set;}
}

[Table("shared_albums")]
public class SharedAlbum {
    [Column("id")]
    public int Id {get; set;}    
    [Column("guest_user_id")]
    public int GuestUserId {get; set;}
    public User GuestUser {get; set;}
    [Column("shared_album_id")]
    public int AlbumId {get; set;}
    public Album Album {get; set;}
}

[Table("storages")]
public class ShotStorage {   
    [Column("id")]
    public int Id {get; set;}
    [Column("user_id")]
    public int UserId {get; set;}
    public User User {get; set;}
    [Column("auth_token")]
    public string AuthToken {get; set;}
    [Column("refresh_token")]
    public string RefreshToken {get; set;}
    [Column("provider")]
    public Provider Provider {get; set;}
    [Column("root")]
    public string Root {get; set;}
    public ICollection<Shot> Shots {get; set;}
}

public enum Provider
{
    Local,
    Yandex,
    Google
}

