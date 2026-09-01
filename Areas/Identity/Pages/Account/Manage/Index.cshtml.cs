using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Cinematron.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cinematron.Areas.Identity.Pages.Account.Manage;

public class IndexModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetUserAsync();
        if (user is null) return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        Input = new InputModel { FullName = user.FullName, Age = user.Age, Gender = user.Gender, ProfileMemo = user.ProfileMemo };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await GetUserAsync();
        if (user is null) return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        if (!ModelState.IsValid) return Page();
        user.FullName = Input.FullName?.Trim(); user.Age = Input.Age; user.Gender = Input.Gender?.Trim(); user.ProfileMemo = Input.ProfileMemo?.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); return Page(); }
        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "Your profile has been updated.";
        return RedirectToPage();
    }

    private Task<ApplicationUser?> GetUserAsync() => userManager.GetUserAsync(User);

    public sealed class InputModel
    {
        [StringLength(120)] public string? FullName { get; set; }
        [Range(13, 120)] public int? Age { get; set; }
        [StringLength(40)] public string? Gender { get; set; }
        [StringLength(2000)] public string? ProfileMemo { get; set; }
    }
}
