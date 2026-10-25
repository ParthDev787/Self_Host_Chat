using Microsoft.EntityFrameworkCore;

namespace API.Data;

public static class DbInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<DataContext>();

            var conn = context.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ConnectionRequests (
                        Id TEXT PRIMARY KEY,
                        FromUsername TEXT NOT NULL,
                        ToUsername TEXT NOT NULL,
                        Message TEXT,
                        Status TEXT NOT NULL,
                        Timestamp TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS UserConnections (
                        Id TEXT PRIMARY KEY,
                        User1 TEXT NOT NULL,
                        User2 TEXT NOT NULL,
                        ConnectedAt TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS ChatMessages (
                        Id TEXT PRIMARY KEY,
                        Sender TEXT NOT NULL,
                        Recipient TEXT NOT NULL,
                        Text TEXT NOT NULL,
                        Timestamp TEXT NOT NULL,
                        Ciphertext TEXT,
                        Iv TEXT,
                        Mac TEXT,
                        EphemeralPublicKey TEXT,
                        Sequence INTEGER DEFAULT 0,
                        IsDelivered INTEGER DEFAULT 1,
                        IsRead INTEGER DEFAULT 0,
                        ReadAt TEXT
                    );
                    CREATE TABLE IF NOT EXISTS UserPublicKeys (
                        Id TEXT PRIMARY KEY,
                        Username TEXT NOT NULL UNIQUE,
                        PublicKeyJwk TEXT NOT NULL,
                        PreKeyJwk TEXT,
                        UpdatedAt TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS RefreshTokens (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL,
                        Token TEXT NOT NULL UNIQUE,
                        ExpiresAt TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        RevokedAt TEXT,
                        ReplacedByToken TEXT
                    );
                    CREATE INDEX IF NOT EXISTS IX_RefreshTokens_Token ON RefreshTokens (Token);
                    CREATE INDEX IF NOT EXISTS IX_RefreshTokens_Username ON RefreshTokens (Username);
                ";
                await cmd.ExecuteNonQueryAsync();

                // Safely ensure new columns exist in ChatMessages if table already existed
                string[] alterQueries = [
                    "ALTER TABLE ChatMessages ADD COLUMN Ciphertext TEXT;",
                    "ALTER TABLE ChatMessages ADD COLUMN Iv TEXT;",
                    "ALTER TABLE ChatMessages ADD COLUMN Mac TEXT;",
                    "ALTER TABLE ChatMessages ADD COLUMN EphemeralPublicKey TEXT;",
                    "ALTER TABLE ChatMessages ADD COLUMN Sequence INTEGER DEFAULT 0;",
                    "ALTER TABLE ChatMessages ADD COLUMN IsDelivered INTEGER DEFAULT 1;",
                    "ALTER TABLE ChatMessages ADD COLUMN IsRead INTEGER DEFAULT 0;",
                    "ALTER TABLE ChatMessages ADD COLUMN ReadAt TEXT;"
                ];
                foreach (var alter in alterQueries)
                {
                    try
                    {
                        cmd.CommandText = alter;
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch { /* Column already exists */ }
                }
            }

            try
            {
                await context.Database.MigrateAsync();
            }
            catch {}
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred during database migration");
        }
    }
}
