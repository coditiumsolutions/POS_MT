using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using POS_MT.Interfaces;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

public class AccountController : Controller
{
    public const string SessionUserNameKey = "UserName";
    public const string SessionTerminalNameKey = "TerminalName";
    public const string SessionBranchKey = "Branch";

    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Cash", "POS");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.SignInAsync(model.UserName, model.Password, cancellationToken);
        if (!result.Succeeded || result.User is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User.UserId),
            new(ClaimTypes.Name, result.User.DisplayName),
            new(ClaimTypes.Role, result.User.Role),
            new("UserName", result.User.UserName)
        };

        if (!string.IsNullOrWhiteSpace(result.User.TerminalName))
            claims.Add(new Claim("TerminalName", result.User.TerminalName));
        if (!string.IsNullOrWhiteSpace(result.User.Branch))
            claims.Add(new Claim("Branch", result.User.Branch));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(14)
                : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);

        HttpContext.Session.SetString(SessionUserNameKey, result.User.UserName ?? string.Empty);
        HttpContext.Session.SetString(SessionTerminalNameKey, result.User.TerminalName ?? string.Empty);
        HttpContext.Session.SetString(SessionBranchKey, result.User.Branch ?? string.Empty);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Cash", "POS");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Remove(SessionUserNameKey);
        HttpContext.Session.Remove(SessionTerminalNameKey);
        HttpContext.Session.Remove(SessionBranchKey);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
