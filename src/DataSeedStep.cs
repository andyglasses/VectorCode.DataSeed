namespace CodedVector.DataSeed;

/// <summary>
/// A step in the data seed process.
/// </summary>
public class DataSeedStep
{
  /// <summary>
  /// The order of the step.
  /// </summary>
  public int Order { get; set; }

  /// <summary>
  /// The name of the step.
  /// </summary>
  public required string Name { get; set; }

  /// <summary>
  /// The item type of the step.
  /// </summary>
  public required string ItemType { get; set; }

  /// <summary>
  /// The items of the step.
  /// </summary>
  public required List<dynamic> Items { get; set; }
}