using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace svema.Data;

public class ApplicationDbContext : DbContext {

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base (options) {
        //super(options);
    }

    public DbSet<Album> Albums {get; set;}
    public DbSet<Shot> Shots {get; set;}
    public DbSet<Location> Location {get; set;}
    public DbSet<Person> Person {get; set;}
    public DbSet<User> User {get; set;}
    public DbSet<Comment> Comment {get; set;}

}

public class Album {
    public int AlbumId {get; set;}
    public string Name {get; set;}
    public Location Location {get; set;}
    public DateTime Date {get; set;}
    public string DatePrecision {get; set;}
    public ICollection<Shot> Shots {get;}
}

public class Shot {
    public int ShotId {get; set;}
    public string Name {get; set;}
    public Album Album {get; set;}    
    public int AlbumId {get; set;}
    public DateTime Date {get; set;}
    public byte[] Preview {get; set;}
    public string SourceUri {get; set;} 
    public Location Location {get; set;}    
        
}

public class Location {
    public int LocationId {get; set;}
    public string Name {get; set;}
    public float Longitude {get; set;}
    public float Latitude {get; set;}
    public int LocationPrecisionMeters {get; set;}
}

public class Person {
    public int PersonId {get; set;}
    public string FirstName {get; set;}
    public string LastName {get; set;} 
}

public class User {
    public int UserId {get; set;}
}

public class Comment {
    public int CommentId {get; set;}
    public User Author {get; set;}
    public int FilmId {get; set;}
    public int ShotId {get; set;}
    public DateTime Timestamp {get; set;}
    public string Text {get; set;}
}
