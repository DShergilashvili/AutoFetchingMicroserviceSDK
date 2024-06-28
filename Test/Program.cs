using AutoFetchingMicroserviceSDK;
using Microsoft.Extensions.DependencyInjection;
using Test;

public class Program
{
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddAutoFetchingSDK(options =>
        {
            options.MicroserviceEndpoints["CompanyService"] = "https://example.com/api/Company";

        });

        services.AddScoped<EmployeeService>();

        var serviceProvider = services.BuildServiceProvider();

        // Simulating JWT token
        var jwtToken = "";

        var exampleService = serviceProvider.GetRequiredService<EmployeeService>();
        var Employee = exampleService.GetEventWithCompanyAsync(Guid.NewGuid(), jwtToken).GetAwaiter().GetResult();

        Console.WriteLine($"FirstName: {Employee.FirstName},LastName: {Employee.FirstName}, Company: {Employee.Company?.Name}");

        Console.ReadLine();
    }
}