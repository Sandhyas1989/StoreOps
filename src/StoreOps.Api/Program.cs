using System.Text.Json.Serialization;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Modules.Reports;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Shared;
using StoreOps.Api.Shared.Errors;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ModelStateErrorFactory.Create);

builder.Services.AddSharedInfrastructure();
builder.Services
    .AddStaffModule()
    .AddActivitiesModule()
    .AddProgrammesModule()
    .AddAlertsModule()
    .AddReportsModule();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new() { Title = "StoreOps API", Version = "v1" }));

var app = builder.Build();

app.UseExceptionHandler();

// API documentation is exposed for local development only, not in the Production container image.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposed so integration tests can bootstrap the app with WebApplicationFactory.</summary>
public partial class Program { }
