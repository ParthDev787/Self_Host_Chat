using System.Text;
using API.Data;
using API.Interface;
using API.Middleware;
using API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Filters;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    // Backend log file (7-day retention)
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(Matching.WithProperty("Source", "Frontend"))
        .WriteTo.File("Logs/backend-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"))
    // Frontend log file (7-day retention)
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(Matching.WithProperty("Source", "Frontend"))
        .WriteTo.File("Logs/frontend-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"))
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddDbContext<DataContext>(opt =>
{
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddCors();
builder.Services.AddScoped<ITokenService, TokenService>();

var tokenKey = builder.Configuration["TokenKey"] 
    ?? throw new Exception("TokenKey not found in configuration");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors(x => x
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithOrigins("http://localhost:4200", "https://localhost:4200", "http://localhost:3000")
    .WithExposedHeaders("X-Correlation-Id"));

// Request logging middleware with correlation ID
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply SQLite migrations automatically on startup
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

app.Run();
