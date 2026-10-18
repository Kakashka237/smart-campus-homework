using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SmartCampus.Canteen.Api.Data;
using SmartCampus.Canteen.Api.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Фаза миграции Price > Amount (Expand | Contract)
var phase = builder.Configuration.GetValue("Migration:ReceiptAmountPhase", MigrationPhase.Expand);
builder.Services.AddSingleton(new MigrationSettings(phase));

// PostgreSQL в проде; InMemory — для локального запуска и тестов без БД
var connectionString = builder.Configuration.GetConnectionString("Campus");
builder.Services.AddDbContext<CanteenDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseInMemoryDatabase("smart-campus-canteen");
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// OpenTelemetry: трассировка входящих запросов + собственный ActivitySource сервиса
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(CampusTelemetry.ServiceName, serviceVersion: CampusTelemetry.ServiceVersion)
        .AddAttributes(new Dictionary<string, object> { ["deployment.change_request"] = "CR-CAMPUS-104" }))
    .WithTracing(tracing => tracing
        .AddSource(CampusTelemetry.ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", version = CampusTelemetry.ServiceVersion, phase = phase.ToString() }));

app.Run();

public partial class Program { }
