using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnalisisPredictivoVentas.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PasswordHasher<Usuario> _hasher = new();

        public AccountController(AppDbContext db) => _db = db;

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginVm model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Email == model.Email && x.Activo);
            if (u != null)
            {
                var vr = _hasher.VerifyHashedPassword(u, u.PasswordHash, model.Password);
                if (vr == PasswordVerificationResult.Success)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
                        new Claim(ClaimTypes.Name, u.Nombres),
                        new Claim(ClaimTypes.Email, u.Email),
                        new Claim(ClaimTypes.Role, u.Rol)
                    };
                    var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(id),
                        new AuthenticationProperties { IsPersistent = model.Recordar });

                    return Redirect(returnUrl ?? "/");
                }
            }

            ModelState.AddModelError("", "Credenciales incorrectas o usuario inactivo");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Denied() => View();
    }

    public class LoginVm
    {
        [Required, EmailAddress] public string Email { get; set; } = null!;
        [Required, DataType(DataType.Password)] public string Password { get; set; } = null!;
        public bool Recordar { get; set; }
    }
}
