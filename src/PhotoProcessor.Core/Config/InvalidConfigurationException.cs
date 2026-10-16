namespace PhotoProcessor.Core.Config;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Thrown when the configuration has errors; lists all of them.</summary>
public sealed class InvalidConfigurationException(IReadOnlyList<string> problems)
    : Exception("Invalid configuration:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "  - " + p)))
{
    /// <summary>Every problem found in the configuration.</summary>
    public IReadOnlyList<string> Problems { get; } = problems;
}
