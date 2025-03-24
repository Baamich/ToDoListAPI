using Microsoft.EntityFrameworkCore;
using ToDoListAPI.Data;
using ToDoListAPI.Models; // Для SmtpSettings
using Microsoft.Extensions.Options; // Для IOptions

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
    options.AddPolicy("AllowBlazor", builder =>
    {
        builder.WithOrigins("http://localhost:8091") 
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Добавляем контроллеры и Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регистрируем SmtpSettings из appsettings.json
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

var app = builder.Build();

// Настраиваем middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ToDoListAPI v1");
    c.RoutePrefix = "swagger";
});

// Конвейер middleware
app.UseRouting();
app.UseCors("AllowBlazor");
app.UseAuthorization();
app.MapControllers();

app.Run();