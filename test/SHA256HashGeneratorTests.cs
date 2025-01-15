namespace VectorCode.DataSeed.Test;

[TestFixture]
public class SHA256HashGeneratorTests
{
  [Test]
  public void GenerateHash_Twice_ShouldProduceSameHash()
  {
    // Arrange
    var generator = new SHA256HashGenerator();
    var fileContent = "This is a test";

    // Act
    var result1 = generator.GenerateHash(fileContent);
    var result2 = generator.GenerateHash(fileContent);

    // Assert
    Assert.That(result1, Is.EqualTo(result2));
  }


  [Test]
  public void GenerateHash_WhenWhitespace_ShouldRemoveWhitespace()
  {
    // Arrange
    var generator = new SHA256HashGenerator();
    var fileContent = "This is a test";
    var noWhitespace = "Thisisatest";
    // Act
    var result = generator.GenerateHash(fileContent);
    var resultNoWhiteSpace = generator.GenerateHash(noWhitespace);

    // Assert
    Assert.That(result, Is.EqualTo(resultNoWhiteSpace));
  }
}
