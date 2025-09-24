using System.Security.Claims;
using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.Models;
using AnalisisPredictivoVentas.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnalisisPredictivoVentas.Controllers
{
    [Authorize(Policy = Permisos.AdministrarUsuarios)]
    public class UsuariosController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PasswordHasher<Usuario> _hasher = new();

        public UsuariosController(AppDbContext db) => _db = db;

        private static string NormalizeEmail(string email)
            => (email ?? string.Empty).Trim().ToLowerInvariant();

        private static bool PassOk(string p) =>
            !string.IsNullOrWhiteSpace(p) && p.Length >= 8 && p.Any(char.IsDigit) && p.Any(char.IsUpper);

        private static readonly string[] RolesValidos = new[]
        {
            Roles.Administrador, Roles.ResponsableCarga, Roles.Empleado
        };

        // ---------- Actions ----------
        public async Task<IActionResult> Index()
        {
            var usuarios = await _db.Usuarios
                .AsNoTracking()
                .OrderBy(u => u.Email)
                .ToListAsync();

            return View(usuarios);
        }

        [HttpGet]
        public IActionResult Create() => View(new Usuario
        {
            Activo = true,
            Rol = Roles.Empleado
        });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Usuario model, string password)
        {
            // 👈 quita la validación del campo que no viene en el form
            ModelState.Remove(nameof(Usuario.PasswordHash));

            // Validar rol
            model.Rol = (model.Rol ?? "").Trim();
            if (!RolesValidos.Contains(model.Rol))
                ModelState.AddModelError(nameof(model.Rol), "Rol inválido.");

            // Validar password
            if (!PassOk(password))
                ModelState.AddModelError("", "La contraseña debe tener al menos 8 caracteres, una mayúscula y un número.");

            // Normalizar email antes de validar
            model.Email = NormalizeEmail(model.Email);

            if (!ModelState.IsValid) return View(model);

            // Unicidad por email (aislamiento serializable evita carrera)
            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var exists = await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == model.Email);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.Email), "Ya existe un usuario con ese email.");
                return View(model);
            }

            model.Nombres = (model.Nombres ?? string.Empty).Trim();
            model.PasswordHash = _hasher.HashPassword(model, password);

            _db.Usuarios.Add(model);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["ok"] = $"Usuario {model.Email} creado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();
            return View(u);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Usuario model, string? newPassword)
        {
            // 👈 igual que en Create, este campo no viene del form
            ModelState.Remove(nameof(Usuario.PasswordHash));

            var u = await _db.Usuarios.FindAsync(model.Id);
            if (u == null) return NotFound();

            if (!ModelState.IsValid) return View(model);

            // Validar rol
            var nuevoRol = (model.Rol ?? "").Trim();
            if (!RolesValidos.Contains(nuevoRol))
            {
                ModelState.AddModelError(nameof(model.Rol), "Rol inválido.");
                return View(model);
            }

            // Evitar dejar el sistema sin Administrador activo
            if (u.Rol == Roles.Administrador && nuevoRol != Roles.Administrador)
            {
                var otrosAdmins = await _db.Usuarios
                    .CountAsync(x => x.Id != u.Id && x.Rol == Roles.Administrador && x.Activo);
                if (otrosAdmins == 0)
                {
                    ModelState.AddModelError("", "Debe existir al menos un Administrador activo.");
                    return View(model);
                }
            }

            u.Nombres = (model.Nombres ?? string.Empty).Trim();
            u.Rol = nuevoRol;
            u.Activo = model.Activo;

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (!PassOk(newPassword))
                {
                    ModelState.AddModelError("", "La nueva contraseña no cumple la política (≥8, mayúscula y número).");
                    return View(model);
                }
                u.PasswordHash = _hasher.HashPassword(u, newPassword);
            }

            await _db.SaveChangesAsync();

            TempData["ok"] = $"Usuario {u.Email} actualizado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActivo(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();

            // No te desactives a ti mismo
            var currentIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(currentIdStr, out var currentId) && currentId == id)
            {
                TempData["Err"] = "No puedes cambiar tu propio estado.";
                return RedirectToAction(nameof(Index));
            }

            if (u.Rol == Roles.Administrador && u.Activo)
            {
                var otrosAdmins = await _db.Usuarios
                    .CountAsync(x => x.Id != u.Id && x.Rol == Roles.Administrador && x.Activo);
                if (otrosAdmins == 0)
                {
                    TempData["Err"] = "No puedes desactivar al único Administrador activo.";
                    return RedirectToAction(nameof(Index));
                }
            }

            u.Activo = !u.Activo;
            await _db.SaveChangesAsync();

            TempData["ok"] = $"Estado de {u.Email} → {(u.Activo ? "Activo" : "Inactivo")}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
