using AnalisisPredictivoVentas.Models;

namespace AnalisisPredictivoVentas.Security
{
    public static class Permisos
    {
        // Definiciones de permisos atómicos (para pantallas o acciones)
        public const string SubirInformacion = "perm:subir-informacion";
        public const string VerDashboardGeneral = "perm:dashboard-general";
        public const string VerTopProductos = "perm:top-productos";
        public const string VerVendedoresCompare = "perm:vendedores-compare";
        public const string AdministrarUsuarios = "perm:admin-usuarios";

        public static IReadOnlyDictionary<string, string[]> PorRol =
            new Dictionary<string, string[]>
            {
                [Roles.Administrador] = new[]
                {
                    SubirInformacion, VerDashboardGeneral,
                    VerTopProductos, VerVendedoresCompare,
                    AdministrarUsuarios
                },
                [Roles.ResponsableCarga] = new[]
                {
                    SubirInformacion
                },
                [Roles.Empleado] = new[]
                {
                    VerDashboardGeneral, VerTopProductos
                }
            };
    }
}

