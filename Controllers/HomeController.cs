using Cinematron.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Cinematron.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index(string? search)
        {
            var movies = new[]
            {
                new MovieCard("The Last Horizon", "Sci-Fi / Adventure", "2026", "2h 08m", "A crew discovers a signal beyond the edge of known space.", "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=700&q=80"),
                new MovieCard("Midnight in Tokyo", "Drama / Mystery", "2025", "1h 52m", "An unexpected meeting changes two lives in a city that never sleeps.", "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=700&q=80"),
                new MovieCard("Neon Run", "Action / Thriller", "2025", "1h 46m", "One night. One city. One chance to outrun the past.", "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=700&q=80")
            };
            var filteredMovies = string.IsNullOrWhiteSpace(search) ? movies : movies.Where(movie => movie.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || movie.Genre.Contains(search, StringComparison.OrdinalIgnoreCase));
            ViewData["Search"] = search;
            return View(filteredMovies);
        }

        public IActionResult Videos(string? search)
        {
            return Index(search);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Upload() => View(new UploadVideoViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upload(UploadVideoViewModel model)
        {
            ValidateUpload(model);

            if (!ModelState.IsValid)
                return View(model);

            TempData["UploadMessage"] = $"{model.Title} was uploaded successfully.";
            return RedirectToAction(nameof(Upload));
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
