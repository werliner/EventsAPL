using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EventsAPL.Data;

public class TursoClient
{
    private readonly HttpClient _httpClient;
    private readonly string _url;

    public TursoClient(string url, string token)
    {
        _url = url;

        // Настраиваем пул соединений: они будут жить дольше, чтобы не тратить время на рукопожатия
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(30)
        };

        _httpClient = new HttpClient(handler);
        // Увеличиваем общий таймаут до 2 минут (Turso иногда тупит из-за расстояния)
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string> ExecuteAsync(string sql)
    {
        var requestBody = new
        {
            requests = new[]
            {
                new { type = "execute", stmt = new { sql = sql } }
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        int retries = 3;
        Exception? lastException = null;

        for (int i = 0; i < retries; i++)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_url}/v2/pipeline", content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                lastException = ex;
                // Если это была не последняя попытка, ждем 2 секунды и пробуем снова
                if (i < retries - 1)
                {
                    await Task.Delay(2000);
                }
            }
        }

        throw new Exception($"Сетевая ошибка после {retries} попыток: {lastException?.Message}", lastException);
    }
}