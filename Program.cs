using Microsoft.EntityFrameworkCore;
using System;
using ToDoListAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// Принудительно задаем порт
var port = Environment.GetEnvironmentVariable("PORT") ?? "8090";
builder.WebHost.UseUrls($"http://+:{port}");

// Подключение к базе данных
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Настройка CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Добавляем контроллеры и Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Отключаем редирект на HTTPS в Docker (Production)
if (!app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Добавляем CORS, затем другие middleware
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();
