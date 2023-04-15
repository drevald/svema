using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
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

public class Album {
    public int AlbumId {get; set;}
    public string Name {get; set;}
    public User User {get; set;}
    public int PreviewId {get; set;}
    public ICollection<Shot> Shots {get;}
    public ICollection<AlbumComment> AlbumComments {get; set;}
}

public class Shot {
    public int ShotId {get; set;}
    public string Name {get; set;}
    public Album Album {get; set;}    
    public int AlbumId {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateStart {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateEnd {get; set;}
    public byte[] Preview {get; set;}
    public string SourceUri {get; set;}
    public Location Location {get; set;}    
    public System.Nullable<int> LocationId {get; set;}    
    public string MD5 {get; set;}
    public ICollection<Person> Persons {get; set;}
    public string ContentType {get; set;}
    public ICollection<ShotComment> ShotComments {get; set;}        
    public ShotStorage Storage {get; set;}
}

public class Location {
    public int Id {get; set;}
    public string Name {get; set;}
    public float Longitude {get; set;}
    public float Latitude {get; set;}
    public int LocationPrecisionMeters {get; set;}
    public int Zoom {get; set;}
}

public class Person {
    public int PersonId {get; set;}
    public string FirstName {get; set;}
    public string LastName {get; set;} 
    public ICollection<Shot> Shots {get; set;}
}

public class User {
    public int UserId {get; set;}
    public string Username {get; set;}
    public string PasswordHash {get; set;}
    public string Email {get; set;}
    public ShotStorage Storage {get; set;}
}

public class ShotComment {
    public int Id {get; set;}
    public User Author {get; set;}
    public int AuthorId {get; set;}
    public string AuthorUsername {get; set;}
    public int ShotId {get; set;}
    public Shot Shot {get; set;}
    public DateTime Timestamp {get; set;}
    public string Text {get; set;}
}

public class AlbumComment {
    public int Id {get; set;}
    public User Author {get; set;}
    public int AuthorId {get; set;}
    public string AuthorUsername {get; set;}
    public Album Album {get; set;}
    public int AlbumId {get; set;}
    public DateTime Timestamp {get; set;}
    public string Text {get; set;}
}

public class ShotStorage {
    public int Id {get; set;}
    public int User {get; set;}
    public string AuthToken {get; set;}
    public string RefreshToken {get; set;}
    public string Provider {get; set;}
    public string Root {get; set;}
}