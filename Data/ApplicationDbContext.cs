using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace svema.Data;

public class ApplicationDbContext : DbContext {

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base (options) {
        //super(options);
    }
    public DbSet<Blog> Blogs { get; set; }    
    public DbSet<Post> Posts { get; set; }    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=svema;Username=postgres;Password=password");

}

public class Blog {
    public int BlogId { get; set; }
    public string Url { get; set; }
    public List<Post> Posts { get; set; }
}

public class Post {
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}