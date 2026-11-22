namespace Loki.Samples.WebApi.Errors;

/// <summary>Thrown on request by the EF Core transaction sample after Loki's save, to show that the caller's rollback undoes it.</summary>
internal sealed class SimulatedFailureException() : Exception("Simulated failure after the Loki save.");
