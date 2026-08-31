using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace hr_sat.Server.Tests.Architecture;

// Enforces the backend slice layout contract (AGENTS.md, docs/agents/dotnet.md):
// slices stay isolated from each other, the domain stays free of feature and
// infrastructure concerns, and *Endpoints classes are the only public surface of Features/.
public sealed class SliceArchitectureTests
{
    private const string RootNamespace = "hr_sat.Server";
    private static readonly Assembly ServerAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_features_or_infrastructure()
    {
        var result = Types.InAssembly(ServerAssembly)
            .That().ResideInNamespaceMatching($@"^{RootNamespace}\.Domain(\.|$)")
            .ShouldNot()
            .HaveDependencyOnAny(
                $"{RootNamespace}.Features",
                $"{RootNamespace}.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, FailingTypes(result));
    }

    [Fact]
    public void Feature_slices_do_not_depend_on_each_other()
    {
        var features = FeatureNames();
        var failures = new List<string>();

        foreach (var feature in features)
        {
            var otherSlices = features
                .Where(other => other != feature)
                .Select(other => $"{RootNamespace}.Features.{other}")
                .ToArray();

            var result = Types.InAssembly(ServerAssembly)
                .That().ResideInNamespaceMatching($@"^{RootNamespace}\.Features\.{feature}(\.|$)")
                .ShouldNot().HaveDependencyOnAny(otherSlices)
                .GetResult();

            if (!result.IsSuccessful)
            {
                failures.Add($"{feature}: {FailingTypes(result)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    [Fact]
    public void Only_endpoint_registrations_are_public_in_features()
    {
        var result = Types.InAssembly(ServerAssembly)
            .That().ResideInNamespaceMatching($@"^{RootNamespace}\.Features(\.|$)")
            .And().ArePublic()
            .Should().HaveNameEndingWith("Endpoints")
            .GetResult();

        Assert.True(result.IsSuccessful, FailingTypes(result));
    }

    private static string[] FeatureNames() =>
        ServerAssembly.GetTypes()
            .Select(type => type.Namespace)
            .Where(ns => ns is not null && ns.StartsWith($"{RootNamespace}.Features.", StringComparison.Ordinal))
            .Select(ns => ns![$"{RootNamespace}.Features.".Length..].Split('.')[0])
            .Where(segment => segment != "Shared")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string FailingTypes(TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}
