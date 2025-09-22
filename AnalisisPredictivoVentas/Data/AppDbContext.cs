using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AnalisisPredictivoVentas.Models;

namespace AnalisisPredictivoVentas.Data
{
    public class AppDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Almacen> Almacenes => Set<Almacen>();
        public DbSet<Empleado> Empleados => Set<Empleado>();
        public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
        public DbSet<ParametrosAbastecimiento> ParametrosAbastecimientos => Set<ParametrosAbastecimiento>();
        public DbSet<VentaCab> VentasCab => Set<VentaCab>();
        public DbSet<VentaDet> VentasDet => Set<VentaDet>();
        public DbSet<VentaPago> VentasPago => Set<VentaPago>();

        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<HechosVentasModels> HechosVentas => Set<HechosVentasModels>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            mb.Entity<VentaCab>()
              .HasMany(v => v.Detalles)
              .WithOne(d => d.VentaCab)
              .HasForeignKey(d => d.VentaCabId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<VentaCab>()
              .HasMany(v => v.Pagos)
              .WithOne(p => p.VentaCab)
              .HasForeignKey(p => p.VentaCabId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<ParametrosAbastecimiento>(e =>
            {
                e.Property(p => p.NivelServicio).HasPrecision(5, 2);     // % 0–100.00
            });

            mb.Entity<VentaCab>(e =>
            {
                e.Property(p => p.Total).HasPrecision(18, 4);
               
            });

            mb.Entity<VentaDet>(e =>
            {
                e.Property(p => p.PrecioUnitario).HasPrecision(18, 4);
                e.Property(p => p.Descuento).HasPrecision(18, 4);
                e.Property(p => p.Subtotal).HasPrecision(18, 4);
            });

            mb.Entity<VentaPago>(e =>
            {
                e.Property(p => p.Monto).HasPrecision(18, 4);
            });

            mb.Entity<MetodoPago>().HasData(
                new MetodoPago { Id = 1, Nombre = "Efectivo", PermiteVuelto = true, Activo = true },
                new MetodoPago { Id = 2, Nombre = "Tarjeta", PermiteVuelto = false, Activo = true },
                new MetodoPago { Id = 3, Nombre = "Transferencia", PermiteVuelto = false, Activo = true }
            );

            mb.Entity<Usuario>(e =>
            {
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.Email).HasMaxLength(120);
                e.Property(x => x.Nombres).HasMaxLength(120);
                e.Property(x => x.Rol).HasMaxLength(40);
            });

            mb.Entity<HechosVentasModels>(e =>
            {
                e.ToTable("HechosVentas", "dbo"); // la tabla ya existe y se llena fuera de EF
                e.HasKey(x => x.Id);
                e.Property(x => x.Cantidad).HasColumnType("decimal(18,3)");
                e.Property(x => x.Neto).HasColumnType("decimal(18,2)");
                e.Property(x => x.Descuento).HasColumnType("decimal(18,2)");
            });


        }
    }
}
