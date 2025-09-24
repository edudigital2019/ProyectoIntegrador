using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.Models;
using AnalisisPredictivoVentas.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace AnalisisPredictivoVentas.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PasswordHasher<Usuario> _hasher = new();

        public AccountController(AppDbContext db) => _db = db;

        private static string NormalizeEmail(string email)
            => (email ?? string.Empty).Trim().ToLowerInvariant();

        // ---------------- Login ----------------

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // Si ya está autenticado, redirige a destino o Home
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginVm());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVm model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var emailNorm = NormalizeEmail(model.Email);

            var u = await _db.Usuarios
                .FirstOrDefaultAsync(x => x.Activo && x.Email.ToLower() == emailNorm);

            if (u != null)
            {
                var vr = _hasher.VerifyHashedPassword(u, u.PasswordHash, model.Password);
                if (vr == PasswordVerificationResult.Success)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
                        new Claim(ClaimTypes.Name, u.Nombres ?? u.Email),
                        new Claim(ClaimTypes.Email, u.Email),
                        new Claim(ClaimTypes.Role, u.Rol)
                    };

                    // Permisos por rol → claims "perm"
                    if (Permisos.PorRol.TryGetValue(u.Rol, out var permisos))
                    {
                        foreach (var p in permisos)
                            claims.Add(new Claim("perm", p));
                    }

                    var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(id),
                        new AuthenticationProperties { IsPersistent = model.Recordar });

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Index", "Home");
                }
            }

            ModelState.AddModelError("", "Credenciales incorrectas o usuario inactivo");
            return View(model);
        }

        // ---------------- Logout ----------------

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Por si quedara la cookie de auth (ajusta el nombre si configuraste uno custom)
            Response.Cookies.Delete(".AspNetCore.Cookies");

            return RedirectToAction("Login");
        }

        // ---------------- Acceso denegado ----------------

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Denied() => View();
    }

    public class LoginVm
    {
        [Required, EmailAddress] public string Email { get; set; } = null!;
        [Required, DataType(DataType.Password)] public string Password { get; set; } = null!;
        public bool Recordar { get; set; }
    }
}
