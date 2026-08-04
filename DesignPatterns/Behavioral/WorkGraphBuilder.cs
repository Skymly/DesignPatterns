using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Builds an immutable <see cref="IWorkGraph{TContext}"/> from manually registered steps.
/// </summary>
/// <typeparam name="TContext">The shared context type for all steps in the graph.</typeparam>
public sealed class WorkGraphBuilder<TContext>
{
    private readonly List<WorkStepRegistration<TContext>> _steps = new();

    /// <summary>
    /// Registers a step with the given id and readiness dependencies.
    /// Validation of the full DAG happens in <see cref="Build"/>.
    /// </summary>
    /// <param name="id">Stable step id (non-whitespace).</param>
    /// <param name="step">The step instance to execute.</param>
    /// <param name="dependsOn">Ids of steps that must complete before this step is ready.</param>
    /// <returns>This builder for chaining.</returns>
    public WorkGraphBuilder<TContext> Add(string id, IWorkStep<TContext> step, params string[] dependsOn)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Step id cannot be null or whitespace.", nameof(id));
        }

        if (step is null)
        {
            throw new ArgumentNullException(nameof(step));
        }

        dependsOn ??= Array.Empty<string>();
        var copy = new string[dependsOn.Length];
        Array.Copy(dependsOn, copy, dependsOn.Length);

        _steps.Add(new WorkStepRegistration<TContext>(id, step, copy));
        return this;
    }

    /// <summary>
    /// Validates the registered DAG and builds an immutable <see cref="IWorkGraph{TContext}"/>.
    /// </summary>
    /// <exception cref="InvalidWorkGraphException">
    /// When the graph is empty, contains a duplicate id, self-dependency, unknown dependency, or cycle.
    /// </exception>
    public IWorkGraph<TContext> Build()
    {
        if (_steps.Count == 0)
        {
            throw new InvalidWorkGraphException(
                "Cannot build an empty work graph. Register at least one step with Add before calling Build.");
        }

        var byId = new Dictionary<string, WorkStepRegistration<TContext>>(StringComparer.Ordinal);
        foreach (var registration in _steps)
        {
            if (byId.ContainsKey(registration.Id))
            {
                throw new InvalidWorkGraphException(
                    $"Duplicate step id '{registration.Id}'. Each step id must be unique within the graph.");
            }

            byId.Add(registration.Id, registration);
        }

        foreach (var registration in _steps)
        {
            foreach (var dependency in registration.DependsOn)
            {
                if (string.IsNullOrWhiteSpace(dependency))
                {
                    throw new InvalidWorkGraphException(
                        $"Step '{registration.Id}' has a null or whitespace DependsOn entry. Use a registered step id.");
                }

                if (string.Equals(dependency, registration.Id, StringComparison.Ordinal))
                {
                    throw new InvalidWorkGraphException(
                        $"Step '{registration.Id}' declares a self-dependency. Remove '{registration.Id}' from DependsOn.");
                }

                if (!byId.ContainsKey(dependency))
                {
                    throw new InvalidWorkGraphException(
                        $"Step '{registration.Id}' depends on unknown step id '{dependency}'. Register '{dependency}' or remove the dependency.");
                }
            }
        }

        var waves = ComputeWaves(byId);
        return new WorkGraph<TContext>(waves);
    }

    private static IReadOnlyList<IReadOnlyList<WorkStepRegistration<TContext>>> ComputeWaves(
        Dictionary<string, WorkStepRegistration<TContext>> byId)
    {
        var remainingIndeegree = new Dictionary<string, int>(byId.Count, StringComparer.Ordinal);
        var successors = new Dictionary<string, List<string>>(byId.Count, StringComparer.Ordinal);

        foreach (var id in byId.Keys)
        {
            remainingIndeegree[id] = 0;
            successors[id] = new List<string>();
        }

        foreach (var registration in byId.Values)
        {
            foreach (var dependency in registration.DependsOn)
            {
                remainingIndeegree[registration.Id]++;
                successors[dependency].Add(registration.Id);
            }
        }

        var ready = new Queue<string>();
        foreach (var pair in remainingIndeegree)
        {
            if (pair.Value == 0)
            {
                ready.Enqueue(pair.Key);
            }
        }

        var waves = new List<IReadOnlyList<WorkStepRegistration<TContext>>>();
        var scheduled = 0;

        while (ready.Count > 0)
        {
            var waveCount = ready.Count;
            var wave = new WorkStepRegistration<TContext>[waveCount];
            for (var i = 0; i < waveCount; i++)
            {
                var id = ready.Dequeue();
                wave[i] = byId[id];
                scheduled++;

                foreach (var successor in successors[id])
                {
                    remainingIndeegree[successor]--;
                    if (remainingIndeegree[successor] == 0)
                    {
                        ready.Enqueue(successor);
                    }
                }
            }

            waves.Add(wave);
        }

        if (scheduled != byId.Count)
        {
            var cyclic = new List<string>();
            foreach (var pair in remainingIndeegree)
            {
                if (pair.Value > 0)
                {
                    cyclic.Add(pair.Key);
                }
            }

            cyclic.Sort(StringComparer.Ordinal);
            throw new InvalidWorkGraphException(
                $"Work graph contains a cycle involving step id(s): {string.Join(", ", cyclic)}. Remove the cyclic DependsOn edge(s).");
        }

        return waves;
    }
}
