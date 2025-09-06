using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnalisisPredictivoVentas.Controllers
{
    [Authorize(Roles = Roles.Administrador)]
    public class UsuariosController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PasswordHasher<Usuario> _hasher = new();

        public UsuariosController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var usuarios = await _db.Usuarios.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
            return View(usuarios);
        }

        [HttpGet]
        public IActionResult Create() => View(new Usuario());

        [HttpPost]
        public async Task<IActionResult> Create(Usuario model, string password)
        {
            if (!ModelState.IsValid) return View(model);
            if (await _db.Usuarios.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Ya existe un usuario con ese email.");
                return View(model);
            }
            model.PasswordHash = _hasher.HashPassword(model, password);
            _db.Usuarios.Add(model);
            await _db.SaveChangesAsync();
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
        public async Task<IActionResult> Edit(Usuario model, string? newPassword)
        {
            var u = await _db.Usuarios.FindAsync(model.Id);
            if (u == null) return NotFound();

            if (!ModelState.IsValid) return View(model);

            u.Nombres = model.Nombres;
            u.Rol = model.Rol;
            u.Activo = model.Activo;

            if (!string.IsNullOrWhiteSpace(newPassword))
                u.PasswordHash = _hasher.HashPassword(u, newPassword);

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActivo(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();
            u.Activo = !u.Activo;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
