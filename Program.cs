using Cinematron.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container. Support both PostgreSQL and SQL Server connection strings.
if (builder.Environment.IsDevelopment())
{
    var localDatabasePath = Path.Combine(builder.Environment.ContentRootPath, "Data", "cinematron-dev.db");
    Directory.CreateDirectory(Path.GetDirectoryName(localDatabasePath)!);

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite($"Data Source={localDatabasePath}"));
}

else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("A database connection string is not configured for production. Set ConnectionStrings:DefaultConnection to the production PostgreSQL connection string.");
    }

    // Choose provider by inspecting the connection string format. Production should provide
    // a PostgreSQL connection when deployed to Railway; SQL Server connection strings (Azure)
    // are still supported but must be targeted to a proper SQL Server instance.
    // Use PostgreSQL provider for production (Railway). If you need SQL Server support,
    // update this branch to configure UseSqlServer and ensure the SQL Server EF Core package is available.
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<Cinematron.Models.ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.ConfigureApplicationCookie(options => options.Cookie.Name = "Cinematron.Auth.v2");
builder.Services.AddControllersWithViews();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                               Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

app.Use((context, next) =>
{
    context.Request.Scheme = "https";
    return next();
});

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (dbContext.Database.IsSqlite())
        {
            dbContext.Database.EnsureCreated();
            EnsureLocalSqliteSchema(dbContext);
            logger.LogInformation("Local SQLite database is ready.");
        }
        else if (dbContext.Database.IsRelational())
        {
            dbContext.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully.");
        }

        if (!dbContext.Jokes.Any())
        {
            dbContext.Jokes.AddRange(
                new Cinematron.Models.Joke { Id = Guid.NewGuid(), Text = "Why did the movie go to therapy? It had too many plot twists.", Author = "Cinematron Editors", PublishedUtc = DateTime.UtcNow },
                new Cinematron.Models.Joke { Id = Guid.NewGuid(), Text = "I wanted to make a movie about clocks, but it was too time-consuming.", Author = "Cinematron Editors", PublishedUtc = DateTime.UtcNow.AddMinutes(-1) });
            dbContext.SaveChanges();
        }

        if (!dbContext.NewsItems.Any())
        {
            dbContext.NewsItems.AddRange(
                new Cinematron.Models.NewsItem { Id = Guid.NewGuid(), Headline = "Welcome to Cinematron", Summary = "Discover, upload, and discuss the stories that matter to you.", Source = "Cinematron", PublishedUtc = DateTime.UtcNow },
                new Cinematron.Models.NewsItem { Id = Guid.NewGuid(), Headline = "Community spotlight", Summary = "Share your latest upload and join the conversation with fellow movie fans.", Source = "Cinematron", PublishedUtc = DateTime.UtcNow.AddMinutes(-1) });
            dbContext.SaveChanges();
        }
    }
    catch (Exception exception)
    {
        logger.LogCritical(exception, "The application could not apply database migrations during startup.");
        throw;
    }

static void EnsureLocalSqliteSchema(ApplicationDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Jokes" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Jokes" PRIMARY KEY,
            "Text" TEXT NOT NULL,
            "Author" TEXT NULL,
            "PublishedUtc" TEXT NOT NULL,
            "IsPublished" INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "NewsItems" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_NewsItems" PRIMARY KEY,
            "Headline" TEXT NOT NULL,
            "Summary" TEXT NOT NULL,
            "Source" TEXT NULL,
            "PublishedUtc" TEXT NOT NULL,
            "IsPublished" INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "VideoReactions" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_VideoReactions" PRIMARY KEY,
            "MovieId" TEXT NOT NULL,
            "UserId" TEXT NOT NULL,
            "Type" INTEGER NOT NULL,
            "CreatedUtc" TEXT NOT NULL,
            CONSTRAINT "FK_VideoReactions_Movies_MovieId" FOREIGN KEY ("MovieId") REFERENCES "Movies" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_VideoReactions_MovieId_UserId" ON "VideoReactions" ("MovieId", "UserId");
        """);

    dbContext.Database.OpenConnection();
    try
    {
        AddSqliteColumnIfMissing(dbContext, "Comments", "IsHighlighted", "INTEGER NOT NULL DEFAULT 0");
        AddSqliteColumnIfMissing(dbContext, "AspNetUsers", "Age", "INTEGER NULL");
        AddSqliteColumnIfMissing(dbContext, "AspNetUsers", "Gender", "TEXT NULL");
        AddSqliteColumnIfMissing(dbContext, "AspNetUsers", "ProfileMemo", "TEXT NULL");
        AddSqliteColumnIfMissing(dbContext, "Movies", "IsPublic", "INTEGER NOT NULL DEFAULT 1");
    }
    finally
    {
        dbContext.Database.CloseConnection();
    }
}

static void AddSqliteColumnIfMissing(ApplicationDbContext dbContext, string tableName, string columnName, string columnDefinition)
{
    using var command = dbContext.Database.GetDbConnection().CreateCommand();
    command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = '{columnName}';";
    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
    {
        dbContext.Database.ExecuteSqlRaw($"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};");
    }
}
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

// Seed admin role and user in production if configured
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<Cinematron.Models.ApplicationUser>>();

        var adminRole = "Admin";
        var adminEmail = "valentinobukovski@gmail.com";

        if (!roleManager.RoleExistsAsync(adminRole).GetAwaiter().GetResult())
        {
            roleManager.CreateAsync(new IdentityRole(adminRole)).GetAwaiter().GetResult();
            logger.LogInformation("Created Admin role.");
        }

        var adminUser = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
        if (adminUser is null)
        {
            var initialPassword = builder.Configuration["Admin:InitialPassword"];
            if (!string.IsNullOrWhiteSpace(initialPassword))
            {
                adminUser = new Cinematron.Models.ApplicationUser { UserName = adminEmail, Email = adminEmail, FullName = "Administrator" };
                var create = userManager.CreateAsync(adminUser, initialPassword).GetAwaiter().GetResult();
                if (create.Succeeded)
                {
                    userManager.AddToRoleAsync(adminUser, adminRole).GetAwaiter().GetResult();
                    logger.LogInformation("Seeded admin user and assigned Admin role.");
                }
                else
                {
                    logger.LogWarning("Failed to create admin user: {Errors}", string.Join(';', create.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogWarning("Admin initial password not configured. Set Admin:InitialPassword in configuration to auto-create admin user.");
            }
        }
        else
        {
            if (!userManager.IsInRoleAsync(adminUser, adminRole).GetAwaiter().GetResult())
            {
                userManager.AddToRoleAsync(adminUser, adminRole).GetAwaiter().GetResult();
                logger.LogInformation("Assigned existing user to Admin role.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding admin role/user.");
    }
}

using (var scope = app.Services.CreateScope())
{
    //var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    //db.Database.Migrate(); 
}

    app.Run();
