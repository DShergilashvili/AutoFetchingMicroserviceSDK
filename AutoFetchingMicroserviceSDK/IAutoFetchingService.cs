namespace AutoFetchingMicroserviceSDK
{
    public interface IAutoFetchingService
    {
        Task<T> FetchAndPopulateAsync<T>(T entity, string? jwtToken) where T : class;
        Task<IEnumerable<T>> FetchAndPopulateAsync<T>(IEnumerable<T> entities, string? jwtToken) where T : class;
    }
}
