using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace AutoFetchingMicroserviceSDK
{
    public class AutoFetchingService : IAutoFetchingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<AutoFetchingOptions> _options;

        public AutoFetchingService(
            IHttpClientFactory httpClientFactory,
            IOptions<AutoFetchingOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options; ;
        }

        public async Task<T> FetchAndPopulateAsync<T>(T entity, string? jwtToken) where T : class
        {
            var properties = typeof(T).GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(AutoFetchAttribute)));

            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<AutoFetchAttribute>();
                var foreignKeyProperty = typeof(T).GetProperty(attribute.ForeignKeyPropertyName);
                var foreignKeyValue = foreignKeyProperty.GetValue(entity);

                if (foreignKeyValue != null)
                {
                    var relatedEntity = await FetchRelatedEntityAsync(attribute.MicroserviceName, property.PropertyType, foreignKeyValue, jwtToken);
                    property.SetValue(entity, relatedEntity);
                }
            }

            return entity;
        }

        public async Task<IEnumerable<T>> FetchAndPopulateAsync<T>(IEnumerable<T> entities, string jwtToken) where T : class
        {
            var tasks = entities.Select(e => FetchAndPopulateAsync(e, jwtToken));
            return await Task.WhenAll(tasks);
        }

        private async Task<object> FetchRelatedEntityAsync(string microserviceName, Type entityType, object id, string? jwtToken)
        {
            if (!_options.Value.MicroserviceEndpoints.TryGetValue(microserviceName, out var endpoint))
            {
                throw new InvalidOperationException($"Endpoint for microservice '{microserviceName}' not found.");
            }

            var client = _httpClientFactory.CreateClient();
            if (jwtToken != null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            }

            var response = await client.GetAsync($"{endpoint}/{id}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize(content, entityType, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }
}
