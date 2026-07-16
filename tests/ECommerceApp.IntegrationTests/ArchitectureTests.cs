using System.Reflection;
using ECommerceApp.Application;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests;

/// <summary>
/// Enforces the dependency rules from the project brief: Domain has zero
/// dependencies, Application depends only on Domain, Infrastructure depends
/// only on Application/Domain, and nothing lower depends on Web.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;
    private static readonly Assembly WebAssembly = typeof(Web.Program).Assembly;

    private static IEnumerable<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!);

    [Fact]
    public void Domain_must_not_reference_any_other_project_assembly()
    {
        var referenced = ReferencedAssemblyNames(DomainAssembly);

        referenced.Should().NotContain(new[]
        {
            ApplicationAssembly.GetName().Name,
            InfrastructureAssembly.GetName().Name,
            WebAssembly.GetName().Name,
        });
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Microsoft.AspNetCore.Mvc")]
    [InlineData("Microsoft.AspNetCore.Mvc.Core")]
    [InlineData("Microsoft.Data.SqlClient")]
    public void Domain_must_not_reference_EfCore_Mvc_or_SqlServer(string forbiddenAssembly)
    {
        ReferencedAssemblyNames(DomainAssembly).Should().NotContain(forbiddenAssembly);
    }

    [Fact]
    public void Application_must_not_reference_Infrastructure_or_Web()
    {
        // Application is allowed to reference Domain, but a project reference that
        // isn't actually used in code is trimmed from the compiled assembly's
        // reference list, so we only assert on what must NOT be there.
        ReferencedAssemblyNames(ApplicationAssembly).Should().NotContain(new[]
        {
            InfrastructureAssembly.GetName().Name,
            WebAssembly.GetName().Name,
        });
    }

    [Fact]
    public void Infrastructure_must_not_reference_Web()
    {
        ReferencedAssemblyNames(InfrastructureAssembly).Should().NotContain(WebAssembly.GetName().Name);
    }

    [Fact]
    public void Infrastructure_must_reference_Application_and_Domain()
    {
        var referenced = ReferencedAssemblyNames(InfrastructureAssembly).ToList();

        referenced.Should().Contain(ApplicationAssembly.GetName().Name);
        referenced.Should().Contain(DomainAssembly.GetName().Name);
    }

    [Fact]
    public void Web_must_reference_Application_and_Infrastructure()
    {
        var referenced = ReferencedAssemblyNames(WebAssembly).ToList();

        referenced.Should().Contain(ApplicationAssembly.GetName().Name);
        referenced.Should().Contain(InfrastructureAssembly.GetName().Name);
    }
}
