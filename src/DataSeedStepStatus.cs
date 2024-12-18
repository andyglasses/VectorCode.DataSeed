namespace VectorCode.DataSeed;

/// <summary>
/// The status of a data seed step
/// </summary>
public enum DataSeedStepStatus
{
  /// <summary>
  /// Unknown or Not Found or Pending
  /// </summary>
  Unknown = 0,
  /// <summary>
  /// Data Seed Step is complete
  /// </summary>
  Complete = 1,
  /// <summary>
  /// Data Seed Step is not run
  /// </summary>
  Pending = 2,
  /// <summary>
  /// Data Seed Step is complete but the file for it is missing
  /// </summary>
  MissingInFile = 3,
  /// <summary>
  /// Data Seed Step is complete bu the hash is now different
  /// </summary>
  ValidationHashMismatch = 4,
}
