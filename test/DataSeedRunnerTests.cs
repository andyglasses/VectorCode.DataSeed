using CodedVector.DataSeed;
using CodedVector.DataSeed.Test.TestDataSeed;
using FluentAssertions;

namespace CodedVector.DddCommon.Test;

[TestFixture]
public class DataSeedRunnerTests
{
  [Test]
  public async Task Run_WhenNothingRun_AndstepsValid_ShouldRunBothSteps()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\Pass");

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
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "112CCB7E0E7A9606FA2AD16C15F75222EEEEFE3E0EC0051905C7FF884395840C"));

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
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(2, "step 2", DataSeedStepStatus.Complete, "A3D8ED02ED737C89B9577AA555202D21361532172F49EAC94142F6811BA0984C"));

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
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\Pass");
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
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\BadJson");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to parse data seed step TestDataSeed\\BadJson\\001.json");
  }

  [Test]
  public async Task Run_WhenJsonEmpty_ShouldThrowFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\EmptyJson");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to parse data seed step TestDataSeed\\EmptyJson\\001.json");
  }

  [Test]
  public async Task Run_WhenFileEmpty_ShouldThrowFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\EmptyFile");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to parse data seed step TestDataSeed\\EmptyFile\\001.json (empty file)");
  }

  [Test]
  public async Task Run_WhenInvalidTypeSpecified_ShouldThrowFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\BadType");

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
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\DuplicateOrder");

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
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\NegativeOrder");

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
    var runner = new TestSeedRunner(repo);
    runner.SetFolder("TestDataSeed\\UnmappedType");

    // Act
    var act = runner.Run;

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Unmapped data seed step types (System.String), make sure they are mapped in GetStep.");
  }
}
