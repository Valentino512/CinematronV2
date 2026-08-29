using Cinematron.Data;
using Cinematron.Models;
using Microsoft.Data.SqlClient;
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
            var filteredMovies = string.IsNullOrWhiteSpace(search) ? movies : movies.Where(movie => movie.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || movie.Genre.Contains(search, StringComparison.OrdinalIgnoreCase));
            ViewData["Search"] = search;
            return View(nameof(Index), filteredMovies.Concat(uploadedMovies));
        }

        [HttpGet]
        public async Task<IActionResult> Videos(string? name, string? genre, DateTime? fromDate, DateTime? toDate, string sort = "newest")
        {
            var query = dbContext.Movies
                .AsNoTracking()
                .Include(movie => movie.Files)
                .AsQueryable();

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

            var video = movie?.Files.FirstOrDefault(file => file.AssetType == "Video");
            if (movie is null || video is null || !video.StoragePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var videoFile = Path.Combine(environment.WebRootPath, video.StoragePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(videoFile))
                return NotFound();

            var poster = movie.Files.FirstOrDefault(file => file.AssetType == "Poster");
            return View(new WatchVideoViewModel(
                movie.Id,
                movie.Title,
                movie.Genre,
                movie.Description,
                poster?.StoragePath ?? "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=700&q=80",
                video.StoragePath,
                video.OriginalFileName));
        }

        public IActionResult Privacy()
        {
            return View();
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
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(ownerId))
                query = query.Where(movie => movie.OwnerId == ownerId);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(movie => movie.Title.Contains(search) || movie.Genre.Contains(search));

            var movies = await query.OrderByDescending(movie => movie.CreatedUtc).ToListAsync();
            return movies.Select(ToMovieCard).ToArray();
        }

        private static MovieCard ToMovieCard(Movie movie)
        {
            var poster = movie.Files.FirstOrDefault(file => file.AssetType == "Poster");
            var video = movie.Files.FirstOrDefault(file => file.AssetType == "Video");
            return new MovieCard(movie.Title, movie.Genre, movie.CreatedUtc.Year.ToString(), "Uploaded", movie.Description, poster?.StoragePath ?? "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=700&q=80", movie.Id, video?.StoragePath);
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
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                builder.Password = "********";
                builder.UserID = string.IsNullOrWhiteSpace(builder.UserID) ? string.Empty : "********";
                return builder.ConnectionString;
            }
            catch (ArgumentException)
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
