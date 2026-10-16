namespace PhotoProcessor.Core.Commands;

using System.Collections.Generic;
using PhotoProcessor.Core.Dating;

internal sealed record CommandBatch(IReadOnlyList<IFileCommand> Commands, DateSource DateSource);
