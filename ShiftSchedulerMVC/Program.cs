using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Data;
using ShiftSchedulerMVC.Models;

var builder = WebApplication.CreateBuilder(args);

// Aplikacja jest polskojęzyczna – wymuszamy pl-PL (np. nazwy miesięcy w kalendarzu)
var plCulture = new System.Globalization.CultureInfo("pl-PL");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = plCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = plCulture;

// Po��czenie do SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ReturnUrlParameter = "ReturnUrl";
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.Redirect("/Account/Login?ReturnUrl=" + context.Request.Path);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.Redirect("/Account/Login");
        return Task.CompletedTask;
    };
    options.SlidingExpiration = true;

});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

//WYMUSZAMY migracj� i seeding r�l + admina
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); //tworzy tabelki
    await DbInitializer.SeedRolesAndAdminAsync(services);
}

app.Run();
