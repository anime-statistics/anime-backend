using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeBackend.Api.Hubs;
using AnimeBackend.Api.Middleware;
using AnimeBackend.Application;
using AnimeBackend.Infrastructure;
using AnimeBackend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Deliberately no static Log.Logger: a per-host logger keeps parallel test
// hosts (and future hosted tooling) from fighting over global state.
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // The wire contract is snake_case in both directions; nulls are
        // dropped to match zod `.optional()` fields being absent.
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Binding failures must leave as `{ "message": ... }` like every other error,
// not as ProblemDetails.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
            ?? "Некорректный запрос";
        return new BadRequestObjectResult(new { message });
    };
});

builder.Services.AddSignalR();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "v1";
    settings.Title = "anime-backend API";
    settings.Version = "v1";
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors("frontend");

// Note attachments are served straight from the storage folder.
var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
var attachmentsRoot = Path.GetFullPath(storage.AttachmentsPath);
Directory.CreateDirectory(attachmentsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(attachmentsRoot),
    RequestPath = storage.PublicPath,
});

app.UseOpenApi();
app.UseSwaggerUi();
app.UseReDoc(settings => settings.Path = "/redoc");

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

MigrateDatabase(app);

app.Run();

static void MigrateDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // SQLite creates the file but not the folder it lives in.
    var connectionString = db.Database.GetConnectionString();
    if (!string.IsNullOrEmpty(connectionString))
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (!string.IsNullOrEmpty(dataSource) && dataSource != ":memory:")
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
    }

    db.Database.Migrate();
}

// WebApplicationFactory in integration tests needs a reachable entry point.
public partial class Program;
