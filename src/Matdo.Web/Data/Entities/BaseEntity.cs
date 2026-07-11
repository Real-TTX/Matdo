namespace Matdo.Web.Data.Entities;

/// <summary>
/// Basisklasse für alle DB-Entitäten. Primärschlüssel ist immer "Id" als BIGINT.
/// Jeder Datensatz führt Audit-Spalten: Create/Update Datum + UserId.
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public long? CreateUserId { get; set; }

    public DateTime UpdateDate { get; set; } = DateTime.UtcNow;
    public long? UpdateUserId { get; set; }
}
