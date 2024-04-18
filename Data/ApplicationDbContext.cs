using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;

namespace Data;

public class ApplicationDbContext : DbContext {

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base (options) {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    protected override void OnModelCreating(ModelBuilder builder) {
        builder.Entity<Shot>(entity => {
            entity.HasIndex(e => e.MD5).IsUnique(true);
        });
    }

    public DbSet<Album> Albums {get; set;}
    public DbSet<Shot> Shots {get; set;}
    public DbSet<Location> Locations {get; set;}
    public DbSet<Person> Persons {get; set;}
    public DbSet<User> Users {get; set;}
    public DbSet<AlbumComment> AlbumComments {get; set;}
    public DbSet<ShotComment> ShotComments {get; set;}
    public DbSet<ShotStorage> ShotStorages {get; set;}

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
    public ICollection<Shot> Shots {get;}
    public ICollection<AlbumComment> AlbumComments {get; set;}
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
    [Column("preview")]
    public byte[] Preview {get; set;}
    [Column("source_uri")]
    public string SourceUri {get; set;}
    public Location Location {get; set;}    
    [Column("location_id")]
    public System.Nullable<int> LocationId {get; set;}    
    [Column("md5")]
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
}

[Table("locations")]
public class Location {
    [Column("id")]
    public int Id {get; set;}
    [Column("name")]
    public string Name {get; set;}
    [Column("longitude")]
    public float Longitude {get; set;}
    [Column("latitude")]
    public float Latitude {get; set;}
    [Column("location_precision_meters")]
    public int LocationPrecisionMeters {get; set;}
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

[Table("storages")]
public class ShotStorage {
    [Column("id")]
    public int Id {get; set;}
    [Column("user_id")]
    public int User {get; set;}
    [Column("auth_token")]
    public string AuthToken {get; set;}
    [Column("refresh_token")]
    public string RefreshToken {get; set;}
    [Column("provider")]
    public string Provider {get; set;}
    [Column("root")]
    public string Root {get; set;}
    public ICollection<Shot> Shots {get; set;}
}