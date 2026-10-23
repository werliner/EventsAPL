using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EventsAPL.Data;

public class TursoClient
{
    private readonly HttpClient _httpClient;
    private readonly string _url;

    public TursoClient(string url, string token)
    {
        _url = url;
        _httpClient = new HttpClient();

        // Настраиваем заголовки авторизации для Turso
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Отправляет SQL-запрос в Turso и возвращает ответ в формате JSON.
    /// </summary>
    public async Task<string> ExecuteAsync(string sql)
    {
        // Turso API v2 ожидает запрос в формате JSON с массивом "requests"
        var requestBody = new
        {
            requests = new[]
            {
                new
                {
                    type = "execute",
                    stmt = new { sql = sql }
                }
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Отправляем POST на /v2/pipeline (стандартный эндпоинт Turso для HTTP)
        var response = await _httpClient.PostAsync($"{_url}/v2/pipeline", content);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}