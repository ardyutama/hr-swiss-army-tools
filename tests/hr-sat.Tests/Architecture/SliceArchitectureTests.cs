using System.Reflection;
using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain;
using hr_sat.Infrastructure;
using NetArchTest.Rules;
using Xunit;

namespace hr_sat.Tests.Architecture;

public sealed class SliceArchitectureTests
{
    private const string ApplicationNamespace = "hr_sat.Application";
    private const string InfrastructureNamespace = "hr_sat.Infrastructure";
    private const string ApiNamespace = "hr_sat.Web.Api";
    private static readonly Assembly DomainAssembly = typeof(Entity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IApplicationDbContext).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_outer_layers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace,
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, FailingTypes(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureNamespace,
                ApiNamespace,
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, FailingTypes(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailingTypes(result));
    }

    [Fact]
    public void Application_feature_slices_do_not_depend_on_each_other()
    {
        var features = FeatureNames();
        Assert.NotEmpty(features);
        var failures = new List<string>();

        foreach (var feature in features)
        {
            var otherSlices = features
                .Where(other => other != feature)
                .Select(other => $"{ApplicationNamespace}.Features.{other}")
                .ToArray();

            var result = Types.InAssembly(ApplicationAssembly)
                .That().ResideInNamespaceMatching($@"^{ApplicationNamespace}\.Features\.{feature}(\.|$)")
                .ShouldNot().HaveDependencyOnAny(otherSlices)
                .GetResult();

            if (!result.IsSuccessful)
            {
                failures.Add($"{feature}: {FailingTypes(result)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    private static string[] FeatureNames() =>
        ApplicationAssembly.GetTypes()
            .Select(type => type.Namespace)
            .Where(ns => ns is not null && ns.StartsWith($"{ApplicationNamespace}.Features.", StringComparison.Ordinal))
            .Select(ns => ns![$"{ApplicationNamespace}.Features.".Length..].Split('.')[0])
            .Where(segment => segment != "Shared")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string FailingTypes(TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}
