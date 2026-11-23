
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitor.Features.Authentication;

namespace Monitor.Pages;

[EnableRateLimiting("login")]
public class LoginModel(
  AdminAuthenticationService authenticationService) : PageModel
{
  [BindProperty]
  public string Username { get; set; } = "";

  [BindProperty]
  public string Password { get; set; } = "";

  public string? ErrorMessage { get; private set; }

  public IActionResult OnGet()
  {
    if (User.Identity?.IsAuthenticated == true)
    {
      return RedirectToPage("/Backups");
    }

    return Page();
  }

  public async Task<IActionResult> OnPostAsync()
  {
    /*  Console.WriteLine($"Submitted username: '{Username}'");
     Console.WriteLine($"Configured username: '{authenticationService.ConfiguredUsername}'");
     Console.WriteLine($"Password hash configured: {authenticationService.IsConfigured}");
     Console.WriteLine($"Password received: {!string.IsNullOrEmpty(Password)}"); */

    if (!authenticationService.ValidateCredentials(Username, Password))
    {
      ErrorMessage = "Invalid username or password.";
      return Page();
    }

    var claims = new[]
    {
      new Claim(ClaimTypes.Name, Username),
      new Claim(ClaimTypes.Role, "Administrator")
    };

    var identity = new ClaimsIdentity(
      claims,
      CookieAuthenticationDefaults.AuthenticationScheme);

    var principal = new ClaimsPrincipal(identity);

    await HttpContext.SignInAsync(
      CookieAuthenticationDefaults.AuthenticationScheme,
      principal);

    return RedirectToPage("/Backups");
  }
}
