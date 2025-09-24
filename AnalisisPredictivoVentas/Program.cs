using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.Import;
using AnalisisPredictivoVentas.Security;
using AnalisisPredictivoVentas.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// DB
var cn = builder.Configuration.GetConnectionString("cn")
    ?? throw new InvalidOperationException("No existe la referencia a la conexion");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(cn)
     .EnableDetailedErrors()
     .EnableSensitiveDataLogging());

// Auth por cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.AccessDeniedPath = "/Account/Denied";
        opt.SlidingExpiration = true;
        // opt.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// AUTORIZACIÓN: solo políticas (SIN FallbackPolicy)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Permisos.AdministrarUsuarios, p => p.RequireClaim("perm", Permisos.AdministrarUsuarios));
    options.AddPolicy(Permisos.SubirInformacion, p => p.RequireClaim("perm", Permisos.SubirInformacion));
    options.AddPolicy(Permisos.VerDashboardGeneral, p => p.RequireClaim("perm", Permisos.VerDashboardGeneral));
    options.AddPolicy(Permisos.VerTopProductos, p => p.RequireClaim("perm", Permisos.VerTopProductos));
    options.AddPolicy(Permisos.VerVendedoresCompare, p => p.RequireClaim("perm", Permisos.VerVendedoresCompare));
});

// Servicios dominio
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IReabastecimientoService, ReabastecimientoService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Migrar + seeding admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await InicializadorUsuarios.Ejecutar(scope.ServiceProvider);
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
