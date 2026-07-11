using System.Threading.RateLimiting;
using Matdo.Web.Data;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
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
builder.Services.AddSingleton<LocalizationService>();
builder.Services.AddScoped<UiPreferences>();
builder.Services.AddScoped<Translator>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<LabelService>();
builder.Services.AddScoped<ShareService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<TodoistImportService>();
builder.Services.AddScoped<AnonymousShareService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SmartInputParser>();
builder.Services.AddScoped<EmailSender>();
builder.Services.AddScoped<PushSender>();
builder.Services.AddHostedService<ReminderBackgroundService>();

// ----- Kalender-Anbindung (ICS + Google/Microsoft OAuth) -----
builder.Services.AddHttpClient("calendar", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Matdo/1.0");
});
// Separater Client für ICS-Abos: KEINE automatischen Redirects (SSRF-Schutz, Redirects werden
// manuell und mit Host-Prüfung verfolgt).
builder.Services.AddHttpClient("ics", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.MaxResponseContentBufferSize = 5 * 1024 * 1024;
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Matdo/1.0");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<Matdo.Web.Services.Calendar.TokenProtector>();
builder.Services.AddScoped<Matdo.Web.Services.Calendar.IcsCalendarReader>();
builder.Services.AddScoped<Matdo.Web.Services.Calendar.ICalendarProvider, Matdo.Web.Services.Calendar.GoogleCalendarProvider>();
builder.Services.AddScoped<Matdo.Web.Services.Calendar.ICalendarProvider, Matdo.Web.Services.Calendar.MicrosoftCalendarProvider>();
builder.Services.AddScoped<Matdo.Web.Services.Calendar.CalendarService>();
builder.Services.AddScoped<Matdo.Web.Services.Calendar.IcalExportService>();
builder.Services.AddHostedService<Matdo.Web.Services.Calendar.CalendarSyncBackgroundService>();

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
    options.Conventions.AllowAnonymousToPage("/Account/Setup");
    options.Conventions.AllowAnonymousToPage("/Public/Board");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});

builder.Services.AddControllers(); // für API-Endpunkte (Push, AJAX)

// Sprachumschaltung: aktuelle Kultur aus dem Sprach-Cookie ableiten (Datums-/Zahlenformat).
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(o =>
{
    var cultures = new[] { new System.Globalization.CultureInfo("de"), new System.Globalization.CultureInfo("en") };
    o.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("de");
    o.SupportedCultures = cultures;
    o.SupportedUICultures = cultures;
    o.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.CustomRequestCultureProvider(ctx =>
    {
        var lang = ctx.Request.Cookies[UiPreferences.LangCookie];
        if (string.IsNullOrWhiteSpace(lang)) lang = "de";
        return Task.FromResult<Microsoft.AspNetCore.Localization.ProviderCultureResult?>(
            new Microsoft.AspNetCore.Localization.ProviderCultureResult(lang));
    }));
});

// Anti-Forgery-Token auch per Header akzeptieren (für fetch/AJAX).
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

// Rate-Limit für den öffentlichen anonymen Freigabe-Link (/s/{token}), partitioniert nach
// Client-IP. Bremst Massen-POSTs (Task-Flut) durch Link-Inhaber, ohne echte Nutzer zu behindern.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("anon-board", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Hinter einem TLS-terminierenden Reverse-Proxy X-Forwarded-Proto/-For auswerten,
// damit Request.IsHttps korrekt ist und das Session-Cookie das Secure-Flag erhält.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                         | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor;
    // In Container-Setups ist die Proxy-IP nicht vorab bekannt.
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Muss früh laufen, damit nachfolgende Middleware das korrekte Schema/IP sieht.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// .webmanifest korrekt ausliefern
var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Datenbank migrieren + Stammdaten anlegen.
await DbInitializer.InitializeAsync(app.Services, app.Logger);

app.Run();
