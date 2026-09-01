using Cinematron.Data;
using Cinematron.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cinematron.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext db;
    public AdminController(ApplicationDbContext db) => this.db = db;

    public async Task<IActionResult> Index()
    {
        // Authorization handled by attribute
        var users = await db.Users.OrderBy(u => u.UserName).ToListAsync();
        var model = new List<AdminUserViewModel>();
        foreach (var u in users)
        {
            var videos = await db.Movies.Where(m => m.OwnerId == u.Id).ToListAsync();
            model.Add(new AdminUserViewModel(u.Id, u.FullName ?? u.UserName ?? u.Email ?? u.Id, u.Email, u.LastActiveUtc, u.BannedUntilUtc, videos));
        }
        var content = new AdminContentViewModel(
            await db.Jokes.AsNoTracking().OrderByDescending(joke => joke.PublishedUtc).Select(joke => new AdminJokeViewModel(joke.Id, joke.Text, joke.Author, joke.IsPublished)).ToListAsync(),
            await db.NewsItems.AsNoTracking().OrderByDescending(item => item.PublishedUtc).Select(item => new AdminNewsViewModel(item.Id, item.Headline, item.Summary, item.Source, item.IsPublished)).ToListAsync());
        ViewData["Users"] = model;
        return View(content);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVideo(Guid id)
    {
        // Authorization handled by attribute
        var movie = await db.Movies.Include(m => m.Files).FirstOrDefaultAsync(m => m.Id == id);
        if (movie is null) return NotFound();
        // Delete files from disk if present
        foreach (var f in movie.Files)
        {
            try { System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", f.StoragePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))); } catch { }
        }
        db.Movies.Remove(movie);
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BanUser(string userId, int days)
    {
        var u = await db.Users.FindAsync(userId);
        if (u is null) return NotFound();
        u.BannedUntilUtc = DateTime.UtcNow.AddDays(days);
        await db.SaveChangesAsync();
        // send message
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var msg = new Message { Id = Guid.NewGuid(), FromUserId = adminId, ToUserId = userId, Text = $"You have been banned for {days} day(s).", CreatedUtc = DateTime.UtcNow };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveJoke(Guid? id, string text, string? author, bool isPublished = true)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
            return RedirectToAction(nameof(Index));
        var joke = id.HasValue ? await db.Jokes.FindAsync(id.Value) : null;
        if (joke is null)
        {
            joke = new Joke { Id = Guid.NewGuid(), PublishedUtc = DateTime.UtcNow };
            db.Jokes.Add(joke);
        }
        joke.Text = text.Trim();
        joke.Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim();
        joke.IsPublished = isPublished;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteJoke(Guid id)
    {
        var joke = await db.Jokes.FindAsync(id);
        if (joke is not null) { db.Jokes.Remove(joke); await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNews(Guid? id, string headline, string summary, string? source, bool isPublished = true)
    {
        if (string.IsNullOrWhiteSpace(headline) || string.IsNullOrWhiteSpace(summary) || headline.Length > 200 || summary.Length > 2000)
            return RedirectToAction(nameof(Index));
        var item = id.HasValue ? await db.NewsItems.FindAsync(id.Value) : null;
        if (item is null)
        {
            item = new NewsItem { Id = Guid.NewGuid(), PublishedUtc = DateTime.UtcNow };
            db.NewsItems.Add(item);
        }
        item.Headline = headline.Trim();
        item.Summary = summary.Trim();
        item.Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        item.IsPublished = isPublished;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNews(Guid id)
    {
        var item = await db.NewsItems.FindAsync(id);
        if (item is not null) { db.NewsItems.Remove(item); await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}

public sealed record AdminUserViewModel(string Id, string DisplayName, string Email, DateTime? LastActiveUtc, DateTime? BannedUntilUtc, IEnumerable<Movie> Movies);
