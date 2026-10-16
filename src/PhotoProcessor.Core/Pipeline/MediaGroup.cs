namespace PhotoProcessor.Core.Pipeline;

using System.Collections.Generic;

internal sealed record MediaGroup(string Stem, IReadOnlyList<string> Paths);
