using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services.Reporting;

/// <summary>The editable parts of a report definition captured by the Report Builder.</summary>
public record ReportDefinitionInput(
    string Name, string Title, string SchemaName, string TableName,
    List<string> Columns, ReportFilterModel Filters, ReportFormat Format);

/// <summary>CRUD for reusable report definitions. On save the data source and columns are re-validated against
/// live database metadata via <see cref="ReportSchemaService"/>, so a definition can only ever reference real,
/// selectable objects.</summary>
public class ReportDefinitionService(
    IDbContextFactory<ApplicationDbContext> dbFactory, ReportSchemaService schema, AuditService auditService)
{
    public async Task<List<ReportDefinition>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ReportDefinitions.OrderBy(d => d.Name).ToListAsync();
    }

    public async Task<ReportDefinition?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ReportDefinitions.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<(bool Success, string? Error, int Id)> CreateAsync(ReportDefinitionInput input, string actor)
    {
        var (ok, error) = await ValidateInputAsync(input, null);
        if (!ok) { return (false, error, 0); }

        var def = new ReportDefinition
        {
            Name = input.Name.Trim(),
            Title = input.Title.Trim(),
            SchemaName = input.SchemaName,
            TableName = input.TableName,
            Format = input.Format,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        def.SetColumns(input.Columns);
        def.SetFilters(input.Filters);

        await using var db = await dbFactory.CreateDbContextAsync();
        db.ReportDefinitions.Add(def);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Create, "ReportDefinition", def.Id,
            $"Created report definition '{def.Name}' on {def.SchemaName}.{def.TableName} ({input.Columns.Count} column(s)).");
        return (true, null, def.Id);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, ReportDefinitionInput input, string actor)
    {
        var (ok, error) = await ValidateInputAsync(input, id);
        if (!ok) { return (false, error); }

        await using var db = await dbFactory.CreateDbContextAsync();
        var def = await db.ReportDefinitions.FirstOrDefaultAsync(d => d.Id == id);
        if (def is null) { return (false, "Report definition not found."); }

        def.Name = input.Name.Trim();
        def.Title = input.Title.Trim();
        def.SchemaName = input.SchemaName;
        def.TableName = input.TableName;
        def.Format = input.Format;
        def.SetColumns(input.Columns);
        def.SetFilters(input.Filters);

        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Update, "ReportDefinition", def.Id,
            $"Updated report definition '{def.Name}' ({input.Columns.Count} column(s)).");
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var def = await db.ReportDefinitions.FirstOrDefaultAsync(d => d.Id == id);
        if (def is null) { return (false, "Report definition not found."); }
        if (await db.ScheduledReports.AnyAsync(s => s.ReportDefinitionId == id))
        {
            return (false, "This definition is used by one or more schedules. Delete those schedules first.");
        }

        db.ReportDefinitions.Remove(def);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Delete, "ReportDefinition", def.Id,
            $"Deleted report definition '{def.Name}'.");
        return (true, null);
    }

    private async Task<(bool Ok, string? Error)> ValidateInputAsync(ReportDefinitionInput input, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) { return (false, "Report name is required."); }
        if (string.IsNullOrWhiteSpace(input.Title)) { return (false, "Report title is required."); }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.ReportDefinitions.AnyAsync(d => d.Name == input.Name.Trim() && d.Id != (existingId ?? 0)))
        {
            return (false, "A report definition with this name already exists.");
        }

        var schemaError = await schema.ValidateAsync(input.SchemaName, input.TableName, input.Columns);
        return schemaError is null ? (true, null) : (false, schemaError);
    }
}
