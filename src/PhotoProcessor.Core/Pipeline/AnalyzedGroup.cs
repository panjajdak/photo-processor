namespace PhotoProcessor.Core.Pipeline;

using System.Collections.Generic;
using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Naming;

internal sealed record AnalyzedGroup(
    string Stem,
    string Directory,
    IReadOnlyList<GroupMember> Members,
    NameContext Context,
    DateSource DateSource,
    string SubSecondSortKey);
