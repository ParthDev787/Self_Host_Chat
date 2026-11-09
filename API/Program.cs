using System.Text;
using System.Threading.RateLimiting;
using API.Data;
using API.Extensions;
using API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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
        .WriteTo.File("Logs/api_backend_.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"))
    // Frontend log file (7-day retention)
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(Matching.WithProperty("Source", "Frontend"))
        .WriteTo.File("Logs/api_frontend_.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"))
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Kestrel Slowloris & Request Body constraints
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 35 * 1024 * 1024; // 35 MB server-wide cap
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30); // Mitigate Slowloris slow headers DoS
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddDbContext<DataContext>(opt =>
{
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddCors();
builder.Services.AddApplicationServices();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = "You are sending requests too quickly. Please wait a moment." }),
            token);
    };

    // Global Rate Limiter: Generous 5,000 requests per minute per IP (allows hundreds of chat messages & live sync without restriction)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5000,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Rate limiter for authentication endpoints: max 150 requests per minute per IP
    options.AddPolicy("AuthLimiter", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 150,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Rate limiter for issue reports: max 10 reports per 5 minutes per IP
    options.AddPolicy("ReportLimiter", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

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
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors(x => x
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithOrigins("http://localhost:4200", "https://localhost:4200", "http://localhost:3000")
    .WithExposedHeaders("X-Correlation-Id"));

// Global exception handling — catches all errors, returns consistent JSON
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Anti-Phishing & Anti-Clickjacking Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'none';");
    await next();
});

// Request logging middleware with correlation ID
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply SQLite migrations automatically on startup
await DbInitializer.InitializeDatabaseAsync(app);


app.Run();
