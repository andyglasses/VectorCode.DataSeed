namespace CodedVector.DataSeed;

/// <summary>
/// Generate a hash for a file
/// </summary>
public interface IFileHashGenerator
{
  /// <summary>
  /// Generate a hash for a file
  /// </summary>
  /// <param name="fileContent"></param>
  /// <returns></returns>
  string GenerateHash(string fileContent);
}
