using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToDoListAPI.Data;
using ToDoListAPI.Models;
using MailKit.Net.Smtp;
using MailKit.Net.Imap; // Добавляем для IMAP
using MailKit.Net.Pop3; // Добавляем для POP3
using MimeKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MailKit; // Для MessageSummaryItems

namespace ToDoListAPI.Controllers
{
    [Route("api/tasks")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<TasksController> _logger;

        public TasksController(AppDbContext context, IOptions<SmtpSettings> smtpSettings, ILogger<TasksController> logger)
        {
            _context = context;
            _smtpSettings = smtpSettings.Value;
            _logger = logger;
        }

        // 1. Получить все задачи
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasks()
        {
            return await _context.Tasks.ToListAsync();
        }

        // 2. Получить задачу по ID
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskItem>> GetTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();
            return task;
        }

        // 3. Добавить новую задачу (с отправкой уведомления на email через SMTP)
        [HttpPost]
        public async Task<ActionResult<TaskItem>> CreateTask(TaskItem task, [FromQuery] string recipientEmail = null)
        {
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(recipientEmail))
            {
                await SendEmailAsync(task.Title, recipientEmail, "Новая задача создана");
            }

            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }

        // 4. Обновить задачу (с отправкой уведомления на email через SMTP)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, TaskItem task, [FromQuery] string recipientEmail = null)
        {
            if (id != task.Id) return BadRequest();

            _context.Entry(task).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(recipientEmail))
            {
                await SendEmailAsync(task.Title, recipientEmail, "Задача обновлена");
            }

            return NoContent();
        }

        // 5. Удалить задачу
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 6. Отправка письма вручную (SMTP)
        [HttpPost("send-email")]
        public async Task<IActionResult> SendEmail(int taskId, string recipientEmail)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null) return NotFound();

            try
            {
                await SendEmailAsync(task.Title, recipientEmail, "Напоминание о задаче");
                _logger.LogInformation("Письмо успешно отправлено на {RecipientEmail} для задачи {TaskId}", recipientEmail, taskId);
                return Ok("Письмо отправлено!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке письма на {RecipientEmail} для задачи {TaskId}", recipientEmail, taskId);
                return StatusCode(500, "Ошибка при отправке письма: " + ex.Message);
            }
        }

        // 7. Проверка входящих писем через IMAP
        [HttpGet("check-inbox-imap")]
        public async Task<IActionResult> CheckInboxImap()
        {
            try
            {
                _logger.LogInformation("Начало проверки входящих писем через IMAP");

                using (var client = new ImapClient())
                {
                    // Подключаемся к IMAP-серверу Mail.ru
                    await client.ConnectAsync("imap.mail.ru", 993, true); // Порт 993 с SSL
                    _logger.LogInformation("Подключение к IMAP-серверу imap.mail.ru:993 успешно");

                    // Аутентификация
                    await client.AuthenticateAsync(_smtpSettings.SenderEmail, _smtpSettings.SenderPassword);
                    _logger.LogInformation("Аутентификация для {SenderEmail} успешна", _smtpSettings.SenderEmail);

                    // Открываем папку "Входящие"
                    var inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadOnly);
                    _logger.LogInformation("Папка Входящие открыта");

                    // Получаем последние 5 писем (или меньше, если их меньше)
                    var messages = await inbox.FetchAsync(0, -1, MessageSummaryItems.Envelope);
                    var result = messages.Take(5).Select(m => new
                    {
                        Subject = m.Envelope.Subject,
                        From = m.Envelope.From.ToString(),
                        Date = m.Envelope.Date
                    }).ToList();

                    _logger.LogInformation("Найдено {Count} писем", result.Count);
                    await client.DisconnectAsync(true);

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке входящих писем через IMAP");
                return StatusCode(500, "Ошибка при проверке входящих: " + ex.Message);
            }
        }

        // 8. Проверка входящих писем через POP3
        [HttpGet("check-inbox-pop3")]
        public async Task<IActionResult> CheckInboxPop3()
        {
            try
            {
                _logger.LogInformation("Начало проверки входящих писем через POP3");

                using (var client = new Pop3Client())
                {
                    // Подключаемся к POP3-серверу Mail.ru
                    await client.ConnectAsync("pop.mail.ru", 995, true); // Порт 995 с SSL
                    _logger.LogInformation("Подключение к POP3-серверу pop.mail.ru:995 успешно");

                    // Аутентификация
                    await client.AuthenticateAsync(_smtpSettings.SenderEmail, _smtpSettings.SenderPassword);
                    _logger.LogInformation("Аутентификация для {SenderEmail} успешна", _smtpSettings.SenderEmail);

                    // Получаем количество писем
                    int messageCount = await client.GetMessageCountAsync();
                    _logger.LogInformation("Найдено {MessageCount} писем", messageCount);

                    // Загружаем последние 5 писем (или меньше, если их меньше)
                    var subjects = new List<object>();
                    for (int i = 0; i < Math.Min(5, messageCount); i++)
                    {
                        var message = await client.GetMessageAsync(i);
                        subjects.Add(new
                        {
                            Subject = message.Subject,
                            From = message.From.ToString(),
                            Date = message.Date
                        });
                    }

                    _logger.LogInformation("Загружено {Count} писем", subjects.Count);
                    await client.DisconnectAsync(true);

                    return Ok(subjects);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке входящих писем через POP3");
                return StatusCode(500, "Ошибка при проверке входящих: " + ex.Message);
            }
        }

        // Вспомогательный метод для отправки писем через SMTP
        private async Task SendEmailAsync(string taskTitle, string recipientEmail, string subjectPrefix)
        {
            _logger.LogInformation("Начало отправки письма на {RecipientEmail}", recipientEmail);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpSettings.SenderName, _smtpSettings.SenderEmail));
            message.To.Add(new MailboxAddress("", recipientEmail));
            message.Subject = $"{subjectPrefix}: {taskTitle}";
            message.Body = new TextPart("plain")
            {
                Text = $"Задача: {taskTitle}\n" +
                       $"Статус: {(subjectPrefix.Contains("создана") ? "Новая" : "Обновлена")}\n" +
                       $"Дата: {DateTime.Now}"
            };

            using (var client = new SmtpClient())
            {
                _logger.LogInformation("Подключение к SMTP-серверу {Host}:{Port}", _smtpSettings.Host, _smtpSettings.Port);
                await client.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, MailKit.Security.SecureSocketOptions.StartTls);

                _logger.LogInformation("Аутентификация для {SenderEmail}", _smtpSettings.SenderEmail);
                await client.AuthenticateAsync(_smtpSettings.SenderEmail, _smtpSettings.SenderPassword);

                _logger.LogInformation("Отправка письма...");
                await client.SendAsync(message);

                _logger.LogInformation("Отключение от SMTP-сервера");
                await client.DisconnectAsync(true);
            }
        }
    }
}
