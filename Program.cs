using Microsoft.EntityFrameworkCore;
using System;
using ToDoListAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// ������������� ������ ����
var port = Environment.GetEnvironmentVariable("PORT") ?? "8090";
builder.WebHost.UseUrls($"http://+:{port}");

// ����������� � ���� ������
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ��������� CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ��������� ����������� � Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ��������� �������� �� HTTPS � Docker (Production)
if (!app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ��������� CORS, ����� ������ middleware
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();
