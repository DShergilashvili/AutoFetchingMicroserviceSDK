using AutoFetchingMicroserviceSDK;

namespace Test
{
    public class Employee
    {
        public Guid Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }

        public Guid CompanyId { get; set; }

        [AutoFetch("CompanyService", nameof(CompanyId))]
        public Company Company { get; set; }
    }
}
