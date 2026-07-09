using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>Wendet Migrationen an und legt Stammdaten (Rollen) an.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatdoDbContext>();

        // Retry, da Postgres im Compose-Stack evtl. noch hochfährt.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(ex, "Datenbank nicht erreichbar (Versuch {Attempt}/10). Neuer Versuch in 3s.", attempt);
                await Task.Delay(3000);
            }
        }

        foreach (var name in new[] { Role.Admin, Role.User })
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
                db.Roles.Add(new Role { Name = name, Description = name == Role.Admin ? "Vollzugriff inkl. Administration" : "Standardbenutzer" });
        }
        await db.SaveChangesAsync();
    }
}
