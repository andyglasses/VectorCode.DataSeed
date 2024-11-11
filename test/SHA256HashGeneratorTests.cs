using FluentAssertions;

namespace CodedVector.DataSeed.Test;

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
    result1.Should().Be(result2);
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
    result.Should().Be(resultNoWhiteSpace);
  }
}
