namespace VectorCode.DataSeed;

/// <summary>
/// Settings to ignore
/// </summary>
/// <param name="HashMismatch">Hash mismatch</param>
/// <param name="OutOfOrder">Run out of order</param>
public record IgnoreDto(bool HashMismatch, bool OutOfOrder);
