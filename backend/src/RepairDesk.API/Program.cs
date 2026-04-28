using Microsoft.EntityFrameworkCore;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.DAL.Persistence;
using Serilog;

#pragma warning disable CA1852 // Program class is referenced by Mvc.Testing

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/repairdesk-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{MachineName}/{ThreadId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

    var connStr = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default not configured.");

    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlServer(connStr, sql =>
            sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "RepairDesk API", Version = "v1" });
    });

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins("http://localhost:5173", "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    var app = builder.Build();

    app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    if (!app.Configuration.GetValue("Database:SkipAutoMigrate", false))
    {
        await DbInitializer.InitializeAsync(app.Services);
    }

    Log.Information("RepairDesk API starting");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RepairDesk API host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
