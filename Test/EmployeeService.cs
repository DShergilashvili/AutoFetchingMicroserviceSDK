using AutoFetchingMicroserviceSDK;

namespace Test
{
    public class EmployeeService
    {
        private readonly IAutoFetchingService _autoFetchingService;

        public EmployeeService(IAutoFetchingService autoFetchingService)
        {
            _autoFetchingService = autoFetchingService;
        }

        public async Task<Employee> GetEventWithCompanyAsync(Guid eventId, string? jwtToken)
        {
            var employee = new Employee { Id = eventId, FirstName = "David", LastName = "Shergilashvili", CompanyId = Guid.Parse("38fde5be-41d9-429d-a1dd-ab7445289099") };

            return await _autoFetchingService.FetchAndPopulateAsync(employee, jwtToken);
        }
    }
}
