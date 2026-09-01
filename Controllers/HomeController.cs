using Cinematron.Data;
using Cinematron.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace Cinematron.Controllers
{
    public class HomeController(ApplicationDbContext dbContext, IWebHostEnvironment environment) : Controller
    {
        public async Task<IActionResult> Index(string? search)
        {
            var movies = GetDemoMovies();
            var uploadedMovies = await GetMovieCardsAsync(search);
            var jokes = await dbContext.Jokes.AsNoTracking().Where(joke => joke.IsPublished).OrderByDescending(joke => joke.PublishedUtc).Take(3).ToListAsync();
            var news = await dbContext.NewsItems.AsNoTracking().Where(item => item.IsPublished).OrderByDescending(item => item.PublishedUtc).Take(3).ToListAsync();
            var filteredMovies = string.IsNullOrWhiteSpace(search) ? movies : movies.Where(movie => movie.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || movie.Genre.Contains(search, StringComparison.OrdinalIgnoreCase));
            ViewData["Search"] = search;
            return View(nameof(Index), new HomeContentViewModel(filteredMovies.Concat(uploadedMovies).ToArray(), jokes, news));
        }

        [HttpGet]
        public async Task<IActionResult> Videos(string? name, string? genre, DateTime? fromDate, DateTime? toDate, string sort = "newest")
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = dbContext.Movies
                .AsNoTracking()
                .Include(movie => movie.Files)
                .Include(movie => movie.Owner)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
                query = query.Where(movie => movie.IsPublic || movie.OwnerId == currentUserId);

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(movie => movie.Title.Contains(name));
            if (!string.IsNullOrWhiteSpace(genre))
                query = query.Where(movie => movie.Genre.Contains(genre));
            if (fromDate.HasValue)
                query = query.Where(movie => movie.CreatedUtc >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(movie => movie.CreatedUtc < toDate.Value.Date.AddDays(1));

            query = sort.ToLowerInvariant() switch
            {
                "oldest" => query.OrderBy(movie => movie.CreatedUtc),
                "name" => query.OrderBy(movie => movie.Title),
                _ => query.OrderByDescending(movie => movie.CreatedUtc)
            };

            var uploadedMovies = (await query.ToListAsync()).Select(ToMovieCard).ToArray();
            var genres = await dbContext.Movies
                .AsNoTracking()
                .Select(movie => movie.Genre)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();
            genres = genres.Concat(GetDemoMovies().Select(movie => movie.Genre)).Distinct().OrderBy(value => value).ToList();

            var demoMovies = GetDemoMovies().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(name))
                demoMovies = demoMovies.Where(movie => movie.Title.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(genre))
                demoMovies = demoMovies.Where(movie => movie.Genre.Contains(genre, StringComparison.OrdinalIgnoreCase));

            var filteredMovies = sort.Equals("name", StringComparison.OrdinalIgnoreCase)
                ? demoMovies.OrderBy(movie => movie.Title)
                : demoMovies;

            return View(new VideoFilterViewModel
            {
                Name = name,
                Genre = genre,
                FromDate = fromDate,
                ToDate = toDate,
                Sort = sort,
                Genres = genres,
                Movies = filteredMovies.Concat(uploadedMovies).ToArray()
            });
        }

        [Authorize]
        public async Task<IActionResult> MyVideos(string? search)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var movies = await GetMovieCardsAsync(search, ownerId);
            ViewData["Search"] = search;
            return View(movies);
        }

        [HttpGet]
        public async Task<IActionResult> Watch(Guid id)
        {
            var movie = await dbContext.Movies
                .AsNoTracking()
                .Include(value => value.Files)
                .FirstOrDefaultAsync(value => value.Id == id);

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (movie is null || (!movie.IsPublic && !User.IsInRole("Admin") && movie.OwnerId != currentUserId))
                return NotFound();

            var video = movie?.Files.FirstOrDefault(file => file.AssetType == "Video");
            if (video is null || !video.StoragePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var videoFile = Path.Combine(environment.WebRootPath, video.StoragePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(videoFile))
                return NotFound();

            var poster = movie.Files.FirstOrDefault(file => file.AssetType == "Poster");
            var comments = await dbContext.Comments
                .AsNoTracking()
                .Where(c => c.MovieId == movie.Id)
                .OrderByDescending(c => c.IsHighlighted)
                .ThenByDescending(c => c.CreatedUtc)
                .ToListAsync();

            var userIds = comments.Select(c => c.UserId).Distinct().ToArray();
            var users = await dbContext.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.UserName ?? u.Email ?? u.Id);

            var commentViewModels = comments.Select(c => new CommentViewModel(c.Id, c.UserId, users.GetValueOrDefault(c.UserId) ?? c.UserId, c.Text, c.CreatedUtc, c.EditedUtc, c.IsHighlighted)).ToArray();
            var reactions = await dbContext.VideoReactions.AsNoTracking().Where(reaction => reaction.MovieId == movie.Id).ToListAsync();
            var reactionCounts = Enum.GetValues<VideoReactionType>().ToDictionary(type => type, type => reactions.Count(reaction => reaction.Type == type));
            var currentReaction = reactions.FirstOrDefault(reaction => reaction.UserId == currentUserId)?.Type;

            return View(new WatchVideoViewModel(
                movie.Id,
                movie.Title,
                movie.Genre,
                movie.Description,
                poster?.StoragePath ?? "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=700&q=80",
                video.StoragePath,
                video.OriginalFileName,
                commentViewModels,
                movie.OwnerId,
                reactionCounts,
                currentReaction));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(Guid movieId, [FromForm] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { error = "Comment text cannot be empty." });

            if (!await dbContext.Movies.AnyAsync(movie => movie.Id == movieId))
                return NotFound(new { error = "The selected movie was not found." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                MovieId = movieId,
                UserId = userId,
                Text = text.Trim(),
                CreatedUtc = DateTime.UtcNow
            };

            dbContext.Comments.Add(comment);
            await dbContext.SaveChangesAsync();

            var user = await dbContext.Users.FindAsync(userId);
            var userName = user?.FullName ?? user?.UserName ?? user?.Email ?? userId;
            var vm = new CommentViewModel(comment.Id, comment.UserId, userName, comment.Text, comment.CreatedUtc, comment.EditedUtc);
            return Json(vm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(Guid id, [FromForm] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { error = "Comment text cannot be empty." });

            var comment = await dbContext.Comments.FindAsync(id);
            if (comment is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (comment.UserId != userId)
                return Forbid();

            comment.Text = text.Trim();
            comment.EditedUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return Ok(new { success = true, editedUtc = comment.EditedUtc });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(Guid id)
        {
            var comment = await dbContext.Comments.FindAsync(id);
            if (comment is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (comment.UserId != userId)
                return Forbid();

            dbContext.Comments.Remove(comment);
            await dbContext.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCommentHighlight(Guid id)
        {
            var comment = await dbContext.Comments.Include(value => value.Movie).FirstOrDefaultAsync(value => value.Id == id);
            if (comment?.Movie is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && comment.Movie.OwnerId != userId)
                return Forbid();

            comment.IsHighlighted = !comment.IsHighlighted;
            await dbContext.SaveChangesAsync();
            return Ok(new { success = true, isHighlighted = comment.IsHighlighted });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetVideoReaction(Guid movieId, VideoReactionType type)
        {
            if (!Enum.IsDefined(type) || !await dbContext.Movies.AnyAsync(movie => movie.Id == movieId))
                return BadRequest(new { error = "Invalid video reaction." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var reaction = await dbContext.VideoReactions.FirstOrDefaultAsync(value => value.MovieId == movieId && value.UserId == userId);
            if (reaction is null)
            {
                reaction = new VideoReaction { Id = Guid.NewGuid(), MovieId = movieId, UserId = userId, Type = type, CreatedUtc = DateTime.UtcNow };
                dbContext.VideoReactions.Add(reaction);
            }
            else if (reaction.Type == type)
            {
                dbContext.VideoReactions.Remove(reaction);
            }
            else
            {
                reaction.Type = type;
            }

            await dbContext.SaveChangesAsync();
            var counts = await dbContext.VideoReactions.Where(value => value.MovieId == movieId).GroupBy(value => value.Type).Select(group => new { Type = group.Key, Count = group.Count() }).ToDictionaryAsync(value => value.Type, value => value.Count);
            return Ok(new { counts, currentReaction = reaction.Type == type && dbContext.Entry(reaction).State != EntityState.Deleted ? type.ToString() : null });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVideo(Guid id)
        {
            var movie = await dbContext.Movies.Include(value => value.Files).FirstOrDefaultAsync(value => value.Id == id);
            if (movie is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && movie.OwnerId != userId)
                return Forbid();

            foreach (var file in movie.Files)
            {
                var path = Path.Combine(environment.WebRootPath, file.StoragePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                try { System.IO.File.Delete(path); } catch { }
            }

            dbContext.Movies.Remove(movie);
            await dbContext.SaveChangesAsync();
            return RedirectToAction(User.IsInRole("Admin") ? nameof(Index) : nameof(MyVideos));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetVideoVisibility(Guid id)
        {
            var movie = await dbContext.Movies.FindAsync(id);
            if (movie is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && movie.OwnerId != userId)
                return Forbid();

            movie.IsPublic = !movie.IsPublic;
            await dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(MyVideos));
        }

        [HttpGet]
        public IActionResult Test()
        {
            var connection = dbContext.Database.GetDbConnection();
            var diagnostics = new DatabaseDiagnosticsViewModel
            {
                EnvironmentName = environment.EnvironmentName,
                Provider = dbContext.Database.ProviderName ?? "Unknown",
                Server = connection.DataSource,
                Database = connection.Database,
                RedactedConnectionString = RedactConnectionString(connection.ConnectionString)
            };

            try
            {
                diagnostics = diagnostics with { CanConnect = dbContext.Database.CanConnect() };
                diagnostics = diagnostics with
                {
                    PendingMigrations = dbContext.Database.GetPendingMigrations().ToArray()
                };
            }
            catch (Exception exception)
            {
                diagnostics = diagnostics with
                {
                    ConnectionError = $"{exception.GetType().Name}: {exception.Message}"
                };
            }

            return View(diagnostics);
        }

        [Authorize]
        [HttpGet]
        public IActionResult Upload() => View(new UploadVideoViewModel());

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(UploadVideoViewModel model)
        {
            ValidateUpload(model);

            if (!ModelState.IsValid)
                return View(model);

            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
                return Challenge();

            var movie = new Movie
            {
                Id = Guid.NewGuid(),
                Title = model.Title.Trim(),
                Genre = model.Genre.Trim(),
                Description = model.Description.Trim(),
                OwnerId = ownerId,
                CreatedUtc = DateTime.UtcNow
            };

            var videoPath = await SaveUploadAsync(model.VideoFile!, movie.Id, "video");
            movie.Files.Add(new MovieFile
            {
                Id = Guid.NewGuid(),
                MovieId = movie.Id,
                AssetType = "Video",
                StoragePath = videoPath,
                OriginalFileName = Path.GetFileName(model.VideoFile!.FileName),
                ContentType = model.VideoFile.ContentType,
                SizeBytes = model.VideoFile.Length,
                UploadedUtc = DateTime.UtcNow
            });

            if (model.PosterFile is not null)
            {
                var posterPath = await SaveUploadAsync(model.PosterFile, movie.Id, "poster");
                movie.Files.Add(new MovieFile
                {
                    Id = Guid.NewGuid(),
                    MovieId = movie.Id,
                    AssetType = "Poster",
                    StoragePath = posterPath,
                    OriginalFileName = Path.GetFileName(model.PosterFile.FileName),
                    ContentType = model.PosterFile.ContentType,
                    SizeBytes = model.PosterFile.Length,
                    UploadedUtc = DateTime.UtcNow
                });
            }

            dbContext.Movies.Add(movie);
            await dbContext.SaveChangesAsync();

            TempData["UploadMessage"] = $"{movie.Title} was uploaded successfully.";
            return RedirectToAction(nameof(Upload));
        }

        private async Task<IReadOnlyList<MovieCard>> GetMovieCardsAsync(string? search, string? ownerId = null)
        {
            var query = dbContext.Movies
                .AsNoTracking()
                .Include(movie => movie.Files)
                .Include(movie => movie.Owner)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(ownerId))
                query = query.Where(movie => movie.OwnerId == ownerId);
            else if (!User.IsInRole("Admin"))
                query = query.Where(movie => movie.IsPublic);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(movie => movie.Title.Contains(search) || movie.Genre.Contains(search));

            var movies = await query.OrderByDescending(movie => movie.CreatedUtc).ToListAsync();
            return movies.Select(ToMovieCard).ToArray();
        }

        private static MovieCard ToMovieCard(Movie movie)
        {
            var poster = movie.Files.FirstOrDefault(file => file.AssetType == "Poster");
            var video = movie.Files.FirstOrDefault(file => file.AssetType == "Video");
            var author = movie.Owner?.FullName ?? movie.Owner?.UserName ?? movie.Owner?.Email ?? "Cinematron";
            return new MovieCard(movie.Title, movie.Genre, movie.CreatedUtc.Year.ToString(), "00:00:00", movie.Description, poster?.StoragePath ?? "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=700&q=80", movie.Id, video?.StoragePath, author, movie.OwnerId, movie.IsPublic);
        }

        private static MovieCard[] GetDemoMovies() =>
        [
            new("The Last Horizon", "Sci-Fi / Adventure", "2026", "2h 08m", "A crew discovers a signal beyond the edge of known space.", "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=700&q=80"),
            new("Midnight in Tokyo", "Drama / Mystery", "2025", "1h 52m", "An unexpected meeting changes two lives in a city that never sleeps.", "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=700&q=80"),
            new("Neon Run", "Action / Thriller", "2025", "1h 46m", "One night. One city. One chance to outrun the past.", "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=700&q=80")
        ];

        private async Task<string> SaveUploadAsync(IFormFile file, Guid movieId, string assetType)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var relativeDirectory = Path.Combine("uploads", "movies", movieId.ToString("N"));
            var directory = Path.Combine(environment.WebRootPath, relativeDirectory);
            Directory.CreateDirectory(directory);

            var fileName = $"{assetType}-{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(directory, fileName);
            await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream);
            return "/" + Path.Combine(relativeDirectory, fileName).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string RedactConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            try
            {
                var sensitiveKeys = new[] { "Password", "Pwd", "User ID", "Username", "User Name" };
                var segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

                for (var index = 0; index < segments.Length; index++)
                {
                    var segment = segments[index];
                    var separatorIndex = segment.IndexOf('=');
                    if (separatorIndex < 0)
                        continue;

                    var key = segment[..separatorIndex].Trim();
                    if (sensitiveKeys.Any(sensitiveKey => string.Equals(sensitiveKey, key, StringComparison.OrdinalIgnoreCase)))
                        segments[index] = $"{key}=********";
                }

                return string.Join(';', segments);
            }
            catch (Exception)
            {
                return "[connection string could not be parsed]";
            }
        }

        private void ValidateUpload(UploadVideoViewModel model)
        {
            const long maximumVideoSize = 500L * 1024 * 1024;
            const long maximumPosterSize = 5L * 1024 * 1024;
            var videoExtensions = new[] { ".mp4", ".webm", ".mov" };
            var posterExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            if (model.VideoFile is null)
                ModelState.AddModelError(nameof(model.VideoFile), "Please select a video file.");
            else
            {
                if (model.VideoFile.Length == 0)
                    ModelState.AddModelError(nameof(model.VideoFile), "The video file cannot be empty.");
                if (model.VideoFile.Length > maximumVideoSize)
                    ModelState.AddModelError(nameof(model.VideoFile), "The video must be smaller than 500 MB.");
                if (!videoExtensions.Contains(Path.GetExtension(model.VideoFile.FileName), StringComparer.OrdinalIgnoreCase))
                    ModelState.AddModelError(nameof(model.VideoFile), "Only MP4, WebM, and MOV videos are supported.");
            }

            if (model.PosterFile is not null)
            {
                if (model.PosterFile.Length == 0 || model.PosterFile.Length > maximumPosterSize)
                    ModelState.AddModelError(nameof(model.PosterFile), "The poster must be between 1 byte and 5 MB.");
                if (!posterExtensions.Contains(Path.GetExtension(model.PosterFile.FileName), StringComparer.OrdinalIgnoreCase))
                    ModelState.AddModelError(nameof(model.PosterFile), "Only JPG, PNG, and WebP posters are supported.");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
