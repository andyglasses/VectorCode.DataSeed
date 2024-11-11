using CodedVector.DataSeed;
using CodedVector.DataSeed.Test.TestDataSeed;
using FluentAssertions;
using Moq;

namespace CodedVector.DddCommon.Test;

[TestFixture]
public class DataSeedRunnerTests
{
  [Test]
  public async Task Run_WhenNothingRun_AndstepsValid_ShouldRunBothSteps()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");

    // Act
    await runner.Run();

    // Assert
    runner.StepOneModels.Should().HaveCount(2);
    runner.StepOneModels[0].Number.Should().Be(1);
    runner.StepOneModels[0].Text.Should().Be("one");
    runner.StepOneModels[1].Number.Should().Be(2);
    runner.StepOneModels[1].Text.Should().Be("two");
    runner.StepTwoModels.Should().HaveCount(2);
    runner.StepTwoModels[0].Price.Should().Be(1.11m);
    runner.StepTwoModels[0].Date.Should().Be(new DateTime(2024,1,1));
    runner.StepTwoModels[1].Price.Should().Be(2.22m);
    runner.StepTwoModels[1].Date.Should().Be(new DateTime(2024, 2, 2));
    repo.Steps.Should().HaveCount(2);
    repo.Steps[1].Name.Should().Be("step 1");
    repo.Steps[1].Status.Should().Be(DataSeedStepStatus.Complete);
    repo.Steps[2].Name.Should().Be("step 2");
    repo.Steps[2].Status.Should().Be(DataSeedStepStatus.Complete);
  }

  [Test]
  public async Task Run_WhenStep1Run_AndStepsValid_ShouldRunStepTwo()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "1"));

    // Act
    await runner.Run();

    // Assert
    runner.StepOneModels.Should().HaveCount(0);
    runner.StepTwoModels.Should().HaveCount(2);
    runner.StepTwoModels[0].Price.Should().Be(1.11m);
    runner.StepTwoModels[0].Date.Should().Be(new DateTime(2024, 1, 1));
    runner.StepTwoModels[1].Price.Should().Be(2.22m);
    runner.StepTwoModels[1].Date.Should().Be(new DateTime(2024, 2, 2));
    repo.Steps.Should().HaveCount(2);
    repo.Steps[1].Name.Should().Be("step 1");
    repo.Steps[1].Status.Should().Be(DataSeedStepStatus.Complete);
    repo.Steps[2].Name.Should().Be("step 2");
    repo.Steps[2].Status.Should().Be(DataSeedStepStatus.Complete);
  }

  [Test]
  public async Task Run_WhenStep1NotRun_AndStep2Run_AndStepsValid_ShouldThrowOutOfOrderError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(2, "step 2", DataSeedStepStatus.Complete, "2"));

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Data seed steps must be run in order.");
  }

  [Test]
  public async Task Run_WhenStep1Run_AndStep1HashDifferent_ShouldThrowChangedContentError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "not-right-hash"));

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Data seed steps have changed since last run (1).");
  }

  [Test]
  public async Task Run_WhenJsonCannotBeSerilised_ShouldThrowFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/BadJson");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().Where(m => m.Message.StartsWith("Failed to parse data seed step"));
  }

  [Test]
  public async Task Run_WhenJsonEmpty_ShouldThrowFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/EmptyJson");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().Where(m => m.Message.StartsWith("Failed to parse data seed step"));
  }

  [Test]
  public async Task Run_WhenFileEmpty_ShouldThrowFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/EmptyFile");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().Where(m => m.Message.StartsWith("Failed to parse data seed step"));
  }

  [Test]
  public async Task Run_WhenInvalidTypeSpecified_ShouldThrowFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/BadType");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Unknown data seed step types (CodedVector.DataSeed.Test.TestDataSeed.NotARealType, CodedVector.DataSeed.Test).");
  }

  [Test]
  public async Task Run_WhenDuplicateOrderFound_ShouldThrowDuplicateOrderError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/DuplicateOrder");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Duplicate data seed step order found (1).");
  }

  [Test]
  public async Task Run_WhenNegativeOrderFound_ShouldThrowBadOrderError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/NegativeOrder");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Data seed step order must be greater than 0.");
  }

  [Test]
  public async Task Run_WhenUnmappedTypeFound_ShouldThrowUnmappedTypeError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/UnmappedType");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Unmapped data seed step types (System.String), make sure they are mapped in GetStep.");
  }
}
