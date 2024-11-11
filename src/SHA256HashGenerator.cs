using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CodedVector.DataSeed;

/// <summary>
/// Generate a hash for a file using SHA256
/// </summary>
public class SHA256HashGenerator : IFileHashGenerator
{
  private static readonly Regex sWhitespace = new Regex(@"\s+", RegexOptions.Compiled);

  /// <inheritdoc/>
  public string GenerateHash(string fileContent)
  {
    var noWhitespace = sWhitespace.Replace(fileContent, "");
    var hashbytes = SHA256.HashData(Encoding.UTF8.GetBytes(noWhitespace));
    StringBuilder sb = new StringBuilder();
    foreach (byte b in hashbytes)
    {
      sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
    }
    return sb.ToString();
  }
}
