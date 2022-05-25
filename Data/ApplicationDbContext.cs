using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace svema.Data;

public class ApplicationDbContext : DbContext {

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base (options) {
        //super(options);
    }

    public DbSet<Film> Films {get; set;}

    public DbSet<Shot> Shots {get; set;}

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=svema;Username=postgres;Password=password");

}

// public class Blog {
//     public int BlogId { get; set; }
//     public string Url { get; set; }
//     // public List<Post> Posts { get; set; }
// }


public class Film {

    public int FilmId {get; set;}

    public string Name {get; set;}

    public Location Location {get; set;}

    public DateTime Date {get; set;}

    public string DatePrecision {get; set;}

}

public class Shot {

    public int ShotId {get; set;}

    public string Name {get; set;}

    public Film Film {get; set;}    

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

class User {

    public int UserId {get; set;}

}

class Comment {

    public int CommentId {get; set;}

    public User Author {get; set;}

    public int FilmId {get; set;}

    public int ShotId {get; set;}

    public DateTime Timestamp {get; set;}

    public string Text {get; set;}

}
