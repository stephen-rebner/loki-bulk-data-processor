using Microsoft.EntityFrameworkCore;

namespace Loki.Samples.WebApi.Data;

internal static class SchemaSetup
{
    private const string ApplyMigrationsKey = "Samples:ApplyMigrationsOnStartup";

    /// <summary>
    /// Applies the EF Core migrations to the SQL Server database when <c>Samples:ApplyMigrationsOnStartup</c> is true, in the
    /// Development environment only (elsewhere, apply them as a deployment step: <c>dotnet ef database update</c> or a script).
    /// </summary>
    public static async Task ApplyMigrationsIfConfiguredAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(ApplyMigrationsKey))
        {
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning("{Setting} is ignored outside the Development environment", ApplyMigrationsKey);
            return;
        }

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SamplesDbContext>().Database.MigrateAsync();
        app.Logger.LogInformation("Applied the EF Core migrations to the SQL Server database");
    }
}
