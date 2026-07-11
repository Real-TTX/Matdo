using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Matdo.Web.Data;

/// <summary>
/// Wird ausschließlich von den EF-Core-Tools (dotnet ef) genutzt, damit Migrationen
/// erzeugt werden können, ohne den Anwendungsstart (inkl. DB-Verbindung) auszuführen.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MatdoDbContext>
{
    public MatdoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MatdoDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=matdo;Username=matdo;Password=matdo",
                npg => npg.MigrationsAssembly("Matdo.Web"))
            .Options;
        return new MatdoDbContext(options);
    }
}
