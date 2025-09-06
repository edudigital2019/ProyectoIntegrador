using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.Import;
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
    });

builder.Services.AddAuthorization();

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
