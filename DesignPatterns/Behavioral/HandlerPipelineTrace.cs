using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Records which handlers ran during a traced <see cref="HandlerPipeline{TContext}"/> invocation.
/// </summary>
public sealed class HandlerPipelineTrace
{
    internal HandlerPipelineTrace(IReadOnlyList<HandlerPipelineStep> steps)
    {
        Steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    /// <summary>
    /// Handler steps in registration order, including handlers that were not reached.
    /// </summary>
    public IReadOnlyList<HandlerPipelineStep> Steps { get; }

    /// <summary>
    /// <see langword="true"/> when any handler short-circuited the pipeline.
    /// </summary>
    public bool WasShortCircuited
    {
        get
        {
            for (var i = 0; i < Steps.Count; i++)
            {
                if (Steps[i].Status == HandlerPipelineStepStatus.ShortCircuited)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// <see langword="true"/> when any handler was skipped due to a guard returning <see langword="false"/>.
    /// </summary>
    public bool WasSkipped
    {
        get
        {
            for (var i = 0; i < Steps.Count; i++)
            {
                if (Steps[i].Status == HandlerPipelineStepStatus.Skipped)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
