using Matdo.Web.Data;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ----- Datenbank (PostgreSQL) -----
var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=matdo;Username=matdo;Password=matdo";

builder.Services.AddDbContext<MatdoDbContext>(opt =>
    opt.UseNpgsql(connectionString, npg => npg.MigrationsAssembly("Matdo.Web")));

// ----- Kern-Dienste -----
// Data-Protection-Schlüssel im Daten-Volume ablegen, damit Anti-Forgery-Tokens
// & Co. auch nach einem Container-Rebuild gültig bleiben.
var keysDir = builder.Configuration["Matdo:KeysDir"];
if (string.IsNullOrWhiteSpace(keysDir))
    keysDir = Path.Combine(builder.Environment.ContentRootPath, "data", "keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("Matdo");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddSingleton<JsonConfigService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<LabelService>();
builder.Services.AddScoped<ShareService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<EmailSender>();
builder.Services.AddScoped<PushSender>();
builder.Services.AddHostedService<ReminderBackgroundService>();

// ----- Authentifizierung / Autorisierung -----
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole("Admin"));

// ----- Razor Pages -----
builder.Services.AddRazorPages(options =>
{
    // Standardmäßig ist Anmeldung erforderlich ...
    options.Conventions.AuthorizeFolder("/");
    // ... außer für ausdrücklich öffentliche Seiten.
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Register");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});

builder.Services.AddControllers(); // für API-Endpunkte (Push, AJAX)

// Anti-Forgery-Token auch per Header akzeptieren (für fetch/AJAX).
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// .webmanifest korrekt ausliefern
var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Datenbank migrieren + Stammdaten anlegen.
await DbInitializer.InitializeAsync(app.Services, app.Logger);

app.Run();
