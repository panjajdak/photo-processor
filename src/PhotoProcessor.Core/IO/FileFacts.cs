namespace PhotoProcessor.Core.IO;

using System;

internal sealed record FileFacts(long Length, DateTime LastWriteTime, DateTime LastWriteTimeUtc);
