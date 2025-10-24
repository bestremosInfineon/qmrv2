using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QMRv2.DBContext;
using QMRv2.Repository.Contexts;
using QMRv2.Repository.Contracts;
using v2.Repository.Contexts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddScoped<ITracingServices, TracingServices>();
builder.Services.AddScoped<IBlockServices, BlockServices>();
builder.Services.AddScoped<IDispositionServices, DispositionServices>();
builder.Services.AddScoped<IAdminConfigServices, AdminConfigServices>();
builder.Services.AddScoped<ILogsServices, LogsServices>();
builder.Services.AddScoped<ICOINServices, COINServices>();
builder.Services.AddScoped<IActionServices, ActionServices>();
builder.Services.AddScoped<IIngresServices, IngresServices>();

builder.Configuration.AddJsonFile("appsettings.json");
builder.Services.AddDbContext<AppDBContext>(options => options.UseOracle(Environment.GetEnvironmentVariable("COIN") ?? builder.Configuration.GetConnectionString("COIN")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QMR v2",
        Version = "v1",
        Description = "API Description" // Optional
    });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Your API v1");
});


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
