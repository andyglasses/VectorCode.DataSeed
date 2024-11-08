namespace CodedVector.DataSeed;

/// <summary>
/// Repository functionality required to support data seeding
/// </summary>
public interface IDataSeedRepository
{
  /// <summary>
  /// Fetch the data seed steps
  /// </summary>
  /// <returns>A list of data seed steps found in the repository</returns>
  Task<List<DataSeedStepDto>> GetDataSeedSteps();

  /// <summary>
  /// Save a data seed step
  /// </summary>
  /// <param name="stepDto">Step to save</param>
  Task SaveDataSeedStep(DataSeedStepDto stepDto);
}
