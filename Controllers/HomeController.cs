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
                new MovieCard("The Last Horizon", "Sci-Fi / Adventure", "2026", "2h 08m", "A crew discovers a signal beyond the edge of known space.", "https://images.unsplash.com/photo-1440404653325-ab127d49abs?auto=format&fit=crop&w=700&q=80"),
                new MovieCard("Midnight in Tokyo", "Drama / Mystery", "2025", "1h 52m", "An unexpected meeting changes two lives in a city that never sleeps.", "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=700&q=80"),
                new MovieCard("Neon Run", "Action / Thriller", "2025", "1h 46m", "One night. One city. One chance to outrun the past.", "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=700&q=80"),
                new MovieCard("The Quiet House", "Horror", "2024", "1h 38m", "Some doors should remain closed after dark.", "https://images.unsplash.com/photo-1500534623283-312aade485b7?auto=format&fit=crop&w=700&q=80"),
                new MovieCard("Wild Coast", "Documentary", "2024", "1h 24m", "A journey through the last untouched shores of the world.", "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=700&q=80"),
                new MovieCard("The Final Act", "Comedy / Drama", "2023", "1h 57m", "The show must go on, even when everything goes wrong.", "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=700&q=80")
            };

            var filteredMovies = string.IsNullOrWhiteSpace(search)
                ? movies
                : movies.Where(movie => movie.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || movie.Genre.Contains(search, StringComparison.OrdinalIgnoreCase));

            ViewData["Search"] = search;
            return View(filteredMovies);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
