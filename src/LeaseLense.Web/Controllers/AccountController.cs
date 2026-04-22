using LeaseLense.Application.Abstractions;
using LeaseLense.Web.Models.Account;
using LeaseLense.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace LeaseLense.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IUserAccountService _userAccountService;
    private readonly IEmailVerificationSender _emailVerificationSender;

    public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IUserAccountService userAccountService,
        IEmailVerificationSender emailVerificationSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userAccountService = userAccountService;
        _emailVerificationSender = emailVerificationSender;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var user = new IdentityUser
        {
            UserName = form.Email.Trim(),
            Email = form.Email.Trim()
        };

        var result = await _userManager.CreateAsync(user, form.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(form);
        }

        await _userAccountService.EnsureRenterForEmailAsync(form.Email, form.DisplayName, user.EmailConfirmed, cancellationToken);

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var callbackUrl = Url.Action(
            action: nameof(VerifyEmail),
            controller: "Account",
            values: new { userId = user.Id, token = encodedToken },
            protocol: Request.Scheme) ?? string.Empty;

        try
        {
            await _emailVerificationSender.SendVerificationEmailAsync(user.Email!, callbackUrl, cancellationToken);
            TempData["RegistrationSuccess"] = "Account created. Check your Gmail inbox to verify your email.";
        }
        catch (Exception)
        {
            TempData["RegistrationError"] = "Account created, but verification email could not be sent. Check Gmail SMTP configuration.";
        }
        return RedirectToAction(nameof(Register));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await _signInManager.PasswordSignInAsync(
            form.Email.Trim(),
            form.Password,
            form.RememberMe,
            lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(form.Email.Trim());
            if (user is not null && !await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError(string.Empty, "Please verify your email before signing in.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
            }
            return View(form);
        }

        if (!string.IsNullOrWhiteSpace(form.ReturnUrl) && Url.IsLocalUrl(form.ReturnUrl))
        {
            return Redirect(form.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(string userId, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            TempData["RegistrationError"] = "Invalid verification link.";
            return RedirectToAction(nameof(Register));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["RegistrationError"] = "Verification user not found.";
            return RedirectToAction(nameof(Register));
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            TempData["RegistrationError"] = "Invalid verification token.";
            return RedirectToAction(nameof(Register));
        }
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
        {
            TempData["RegistrationError"] = "Email verification failed. Please request a new link.";
            return RedirectToAction(nameof(Register));
        }

        await _userAccountService.EnsureRenterForEmailAsync(user.Email ?? string.Empty, null, emailVerified: true, cancellationToken);
        TempData["RegistrationSuccess"] = "Email verified. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
