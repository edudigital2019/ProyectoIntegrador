using System.ComponentModel.DataAnnotations;

namespace AnalisisPredictivoVentas.Models
{
    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string ResponsableCarga = "ResponsableCarga";
        public const string Empleado = "Empleado";
    }

    public class Usuario
    {
        public int Id { get; set; }

        [Required, EmailAddress, StringLength(120)]
        public string Email { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        [Required, StringLength(120)]
        public string Nombres { get; set; } = null!;

        [Required, StringLength(40)]
        public string Rol { get; set; } = Roles.Empleado;

        public bool Activo { get; set; } = true;
    }
}
