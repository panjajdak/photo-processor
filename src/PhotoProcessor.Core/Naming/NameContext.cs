namespace PhotoProcessor.Core.Naming;

using System;

internal sealed record NameContext(string Type, DateTime Date, string? Device, string? Make, string? Model, string? Original);
