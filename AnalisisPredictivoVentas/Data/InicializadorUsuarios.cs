using AnalisisPredictivoVentas.Models;
using Microsoft.AspNetCore.Identity; // solo para PasswordHasher
using Microsoft.EntityFrameworkCore;

namespace AnalisisPredictivoVentas.Data
{
    public static class InicializadorUsuarios
    {
        public static async Task Ejecutar(IServiceProvider sp)
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var hasher = new PasswordHasher<Usuario>();

            var email = "admin@demo.local";
            var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email);
            if (u == null)
            {
                u = new Usuario
                {
                    Email = email,
                    Nombres = "Administrador del Sistema",
                    Rol = Roles.Administrador,
                    Activo = true
                };
                u.PasswordHash = hasher.HashPassword(u, "Admin!123");
                db.Usuarios.Add(u);
                await db.SaveChangesAsync();
            }
        }
    }
}
