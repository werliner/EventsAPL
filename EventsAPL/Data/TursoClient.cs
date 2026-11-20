using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EventsAPL.Data;

public class TursoClient
{
    private readonly HttpClient _httpClient;
    private readonly string _workerUrl;

    /// <summary>
    /// Принимает ТОЛЬКО URL Cloudflare Worker.
    /// Токен НЕ нужен — Worker сам подставляет его из секретов.
    /// </summary>
    public TursoClient(string workerUrl)
    {
        // Убираем лишний слэш в конце, если он есть
        _workerUrl = workerUrl.TrimEnd('/');

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(30)
        };

        _httpClient = new HttpClient(handler);
        // Таймаут 120 секунд — на случай медленного интернета или холодного старта Worker'а
        _httpClient.Timeout = TimeSpan.FromSeconds(120);

        Debug.WriteLine($"[TursoClient] Инициализирован с URL: {_workerUrl}");
    }

    /// <summary>
    /// Выполняет SQL-запрос через Cloudflare Worker.
    /// Имеет 3 попытки с задержкой для защиты от нестабильной сети.
    /// </summary>
    public async Task<string> ExecuteAsync(string sql)
    {
        // Логирование запроса (видно в окне Output при отладке)
        Debug.WriteLine($"[TursoClient] SQL ({sql.Length} симв.): {(sql.Length > 200 ? sql.Substring(0, 200) + "..." : sql)}");

        var requestBody = new
        {
            requests = new[]
            {
                new { type = "execute", stmt = new { sql = sql } }
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);

        const int maxRetries = 3;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Каждый раз создаём новый контент, иначе будет ошибка "контент уже отправлен"
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.PostAsync(_workerUrl, content);
                sw.Stop();

                Debug.WriteLine($"[TursoClient] Попытка {attempt}: ответ за {sw.ElapsedMilliseconds}мс, статус {(int)response.StatusCode} ({response.StatusCode})");

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[TursoClient] Успех, получено {result.Length} символов");
                return result;
            }
            catch (OperationCanceledException)
            {
                // Таймаут запроса
                lastException = new TimeoutException($"Запрос превысил таймаут {_httpClient.Timeout.TotalSeconds} сек");
                Debug.WriteLine($"[TursoClient] Попытка {attempt}: ТАЙМАУТ");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                Debug.WriteLine($"[TursoClient] Попытка {attempt}: ошибка сети - {ex.Message}");
            }
            catch (Exception ex)
            {
                // Неизвестная ошибка — не ретраим, сразу пробрасываем
                Debug.WriteLine($"[TursoClient] Критическая ошибка: {ex.Message}");
                throw;
            }

            // Если это не последняя попытка — ждём перед повтором
            if (attempt < maxRetries)
            {
                var delay = 1000 * attempt; // 1 сек, 2 сек
                Debug.WriteLine($"[TursoClient] Ждём {delay}мс перед повтором...");
                await Task.Delay(delay);
            }
        }

        // Все попытки исчерпаны
        throw new Exception($"Не удалось связаться с базой данных после {maxRetries} попыток. " +
                            $"Проверьте интернет-соединение или включите VPN.\n\n" +
                            $"Последняя ошибка: {lastException?.Message}",
                            lastException);
    }
}