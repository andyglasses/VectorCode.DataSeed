namespace VectorCode.DataSeed;

/// <summary>
/// Model for holding basic data about a data seed step
/// </summary>
/// <param name="Order">Execution order of the step</param>
/// <param name="Name">Name of the step</param>
/// <param name="Status">The status of the step</param>
/// <param name="ValidationHash">Hash of the data to validate the step</param>
public record DataSeedStepDto(int Order, string Name, DataSeedStepStatus Status, string ValidationHash);
