using Microsoft.EntityFrameworkCore;
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
builder.Services.AddDbContext<AppDBContext>(options => options.UseOracle(builder.Configuration.GetConnectionString("COIN")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
