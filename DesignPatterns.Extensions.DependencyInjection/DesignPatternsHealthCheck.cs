using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DesignPatterns.Extensions.DependencyInjection;

/// <summary>
/// A health check that verifies all DesignPatterns service registrations
/// can be resolved from the DI container at runtime.
/// </summary>
/// <remarks>
/// The list of service types to check is captured at registration time by
/// <see cref="DesignPatternsServiceCollectionExtensions.AddDesignPatternsHealthChecks"/>.
/// At check time, each service type is resolved via <see cref="IServiceProvider.GetService"/>
/// (non-throwing). Any unresolvable service is reported as <see cref="HealthStatus.Unhealthy"/>.
/// </remarks>
internal sealed class DesignPatternsHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<Type> _serviceTypes;

    public DesignPatternsHealthCheck(
        IServiceProvider serviceProvider,
        DesignPatternsHealthCheckOptions options)
    {
        _serviceProvider = serviceProvider;
        _serviceTypes = options.ServiceTypes;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_serviceTypes.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "No DesignPatterns service registrations found."));
        }

        var failures = new List<string>();
        var resolved = 0;

        foreach (var serviceType in _serviceTypes)
        {
            try
            {
                // Use GetService (non-throwing) to avoid crashing the health check.
                var service = _serviceProvider.GetService(serviceType);
                if (service is null)
                {
                    failures.Add(serviceType.FullName ?? serviceType.Name);
                }
                else
                {
                    resolved++;
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{serviceType.FullName ?? serviceType.Name}: {ex.Message}");
            }
        }

        if (failures.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                $"All {resolved} DesignPatterns service(s) resolved successfully."));
        }

        var failureList = string.Join("; ", failures);
        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"{failures.Count}/{_serviceTypes.Count} DesignPatterns service(s) unresolvable: {failureList}"));
    }
}

/// <summary>
/// Options passed to <see cref="DesignPatternsHealthCheck"/> containing the
/// list of DesignPatterns service types to verify.
/// </summary>
internal sealed class DesignPatternsHealthCheckOptions
{
    public IReadOnlyList<Type> ServiceTypes { get; }

    public DesignPatternsHealthCheckOptions(IReadOnlyList<Type> serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}

internal static class DesignPatternsServiceTypeScanner
{
    private static readonly string[] DesignPatternsNamespaces =
    {
        "DesignPatterns.Behavioral",
        "DesignPatterns.Creational",
        "DesignPatterns.Structural",
        "DesignPatterns.Extensions.DependencyInjection",
    };

    internal static IReadOnlyList<Type> Scan(IServiceCollection services)
    {
        var result = new List<Type>();
        var seen = new HashSet<Type>();

        foreach (var descriptor in services)
        {
            var serviceType = descriptor.ServiceType;
            if (serviceType is null)
            {
                continue;
            }

            var ns = serviceType.Namespace;
            if (ns is null)
            {
                continue;
            }

            if (!IsDesignPatternsNamespace(ns))
            {
                continue;
            }

            if (seen.Add(serviceType))
            {
                result.Add(serviceType);
            }
        }

        return result;
    }

    private static bool IsDesignPatternsNamespace(string ns)
    {
        foreach (var prefix in DesignPatternsNamespaces)
        {
            if (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
