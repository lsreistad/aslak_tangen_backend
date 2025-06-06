using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChessBackend;
using Microsoft.AspNetCore.Authentication;


// Initialize SQLite provider
SQLitePCL.Batteries.Init();

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddDbContext<ChessClubContext>(opt =>
    opt.UseSqlite("Data Source=chessclub.db"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "chessclub",
            ValidAudience = "chessclub_users",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("super_secret_key_123!"))
        };
    });


builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add session and cookie authentication services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.LoginPath = "/api/login";
        options.AccessDeniedPath = "/api/access-denied";
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// API endpoints


app.MapPost("/api/register", async (Member member, ChessClubContext db) =>
{
    // Hash the password (example using SHA256)
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    var passwordBytes = Encoding.UTF8.GetBytes(member.PasswordHash);
    member.PasswordHash = Convert.ToBase64String(sha256.ComputeHash(passwordBytes));

    db.Members.Add(member);
    await db.SaveChangesAsync();
    return Results.Created($"/api/members/{member.Id}", member);
});

// Login endpoint with cookie-based session
app.MapPost("/api/login", async (ChessClubContext db, HttpContext ctx, string username, string password) =>
{
    var member = await db.Members.FirstOrDefaultAsync(m => m.Username == username);
    if (member == null) return Results.Unauthorized();

    // Verify password
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    var passwordBytes = Encoding.UTF8.GetBytes(password);
    var hashedPassword = Convert.ToBase64String(sha256.ComputeHash(passwordBytes));

    if (member.PasswordHash != hashedPassword) return Results.Unauthorized();

    // Set session data
    ctx.Session.SetString("Username", member.Username);
    ctx.Session.SetInt32("MemberId", member.Id);

    // Set authentication cookie
    await ctx.SignInAsync("CookieAuth", new ClaimsPrincipal(
        new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, member.Username) }, "CookieAuth")));

    return Results.Ok(new { message = "Login successful" });
});

// Logout endpoint
app.MapPost("/api/logout", async (HttpContext ctx) =>
{
    ctx.Session.Clear();
    await ctx.SignOutAsync("CookieAuth");
    return Results.Ok(new { message = "Logout successful" });
});

// Members
app.MapGet("/api/members", async (ChessClubContext db, HttpContext ctx) =>
{
    var isAdmin = ctx.User.IsInRole("Admin");
    return await db.Members
        .Where(m => isAdmin || (!m.IsPrivate && m.Age > 15))
        .ToListAsync();
});

app.MapPost("/api/members", [Authorize] async (Member member, ChessClubContext db) =>
{
    db.Members.Add(member);
    await db.SaveChangesAsync();
    return Results.Created($"/api/members/{member.Id}", member);
});

// Events
app.MapGet("/api/events", async (ChessClubContext db, [FromQuery] bool includePast = false) =>
{
    var now = DateTime.UtcNow;
    return await db.Events
        .Where(e => includePast || e.Date >= now)
        .OrderBy(e => e.Date)
        .ToListAsync();
});

app.MapPost("/api/events", [Authorize] async (Event evt, ChessClubContext db) =>
{
    db.Events.Add(evt);
    await db.SaveChangesAsync();
    return Results.Created($"/api/events/{evt.Id}", evt);
});

// Articles
app.MapGet("/api/articles", async (ChessClubContext db) =>
{
    return await db.Articles.OrderByDescending(a => a.Published).ToListAsync();
});

app.MapPost("/api/articles", [Authorize] async (Article article, ChessClubContext db) =>
{
    article.Published = DateTime.UtcNow;
    db.Articles.Add(article);
    await db.SaveChangesAsync();
    return Results.Created($"/api/articles/{article.Id}", article);
});

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChessClubContext>();
    db.Database.Migrate();
}

app.Run();