using Microsoft.EntityFrameworkCore;

namespace ChessBackend;


// Models
public class Member
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public bool IsPrivate { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = ""; // Store hashed passwords
}

public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime Date { get; set; }
    public string Location { get; set; } = "";
}

public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Published { get; set; }
}

public class ChessClubContext : DbContext
{
    public ChessClubContext(DbContextOptions<ChessClubContext> options) : base(options) { }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Article> Articles => Set<Article>();
}