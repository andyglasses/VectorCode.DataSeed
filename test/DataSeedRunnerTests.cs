using VectorCode.DataSeed.Test.TestDataSeed;
using Moq;
using VectorCode.Common;

namespace VectorCode.DataSeed.Test;

[TestFixture]
public class DataSeedRunnerTests
{
  [Test]
  public async Task Run_WhenNothingRun_AndStepsValid_ShouldRunBothSteps()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");

    // Act
    await runner.Run();

    // Assert
    Assert.That(runner.StepOneModels, Has.Exactly(2).Items);
    Assert.That(runner.StepOneModels[0].Number, Is.EqualTo(1));
    Assert.That(runner.StepOneModels[0].Text, Is.EqualTo("one"));
    Assert.That(runner.StepOneModels[1].Number, Is.EqualTo(2));
    Assert.That(runner.StepOneModels[1].Text, Is.EqualTo("two"));
    Assert.That(runner.StepTwoModels, Has.Exactly(2).Items);
    Assert.That(runner.StepTwoModels[0].Price, Is.EqualTo(1.11m));
    Assert.That(runner.StepTwoModels[0].Date, Is.EqualTo(new DateTime(2024, 1, 1)));
    Assert.That(runner.StepTwoModels[1].Price, Is.EqualTo(2.22m));
    Assert.That(runner.StepTwoModels[1].Date, Is.EqualTo(new DateTime(2024, 2, 2)));
    Assert.That(repo.Steps, Has.Exactly(2).Items);
    Assert.That(repo.Steps[1].Name, Is.EqualTo("step 1"));
    Assert.That(repo.Steps[1].Status, Is.EqualTo(DataSeedStepStatus.Complete));
    Assert.That(repo.Steps[2].Name, Is.EqualTo("step 2"));
    Assert.That(repo.Steps[2].Status, Is.EqualTo(DataSeedStepStatus.Complete));
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
    Assert.That(runner.StepOneModels, Has.Exactly(0).Items);
    Assert.That(runner.StepTwoModels, Has.Exactly(2).Items);
    Assert.That(runner.StepTwoModels[0].Price, Is.EqualTo(1.11m));
    Assert.That(runner.StepTwoModels[0].Date, Is.EqualTo(new DateTime(2024, 1, 1)));
    Assert.That(runner.StepTwoModels[1].Price, Is.EqualTo(2.22m));
    Assert.That(runner.StepTwoModels[1].Date, Is.EqualTo(new DateTime(2024, 2, 2)));
    Assert.That(repo.Steps, Has.Exactly(2).Items);
    Assert.That(repo.Steps[1].Name, Is.EqualTo("step 1"));
    Assert.That(repo.Steps[1].Status, Is.EqualTo(DataSeedStepStatus.Complete));
    Assert.That(repo.Steps[2].Name, Is.EqualTo("step 2"));
    Assert.That(repo.Steps[2].Status, Is.EqualTo(DataSeedStepStatus.Complete));
  }

  [Test]
  public async Task Run_WhenStep1NotRun_AndStep2Run_AndStepsValid_ShouldReturnOutOfOrderError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(2, "step 2", DataSeedStepStatus.Complete, "2"));

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code == "OutOfOrder" && e.Key == nameof(DataSeedStepDto.Order)));
  }

  [Test]
  public async Task Run_WhenStep1Run_AndStep1HashDifferent_ShouldReturnHashMismatchError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "not-right-hash"));

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith("Mismatch", StringComparison.Ordinal) && e.Key == nameof(DataSeedStepDto.ValidationHash)));
  }

  [Test]
  public async Task Run_WhenJsonCannotBeSerialized_ShouldReturnFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/BadJson");

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith("FailedToParse:", StringComparison.Ordinal) && e.Key.EndsWith("001.json", StringComparison.Ordinal)));
  }

  [Test]
  public async Task Run_WhenJsonEmpty_ShouldReturnFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/EmptyJson");

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith("FailedToParse:", StringComparison.Ordinal) && e.Key.EndsWith("001.json", StringComparison.Ordinal)));
  }

  [Test]
  public async Task Run_WhenFileEmpty_ShouldReturnFailedToParseStepError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/EmptyFile");

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code == "EmptyFile" && e.Key.EndsWith("001.json", StringComparison.Ordinal)));
  }

  [Test]
  public async Task Run_WhenInvalidTypeSpecified_ShouldReturnNotFoundTypeError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/BadType");

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith(CommonCodes.NotFound, StringComparison.Ordinal) && e.Key == nameof(DataSeedStep.ItemType)));
  }

  [Test]
  public async Task Run_WhenDuplicateOrderFound_ShouldReturnDuplicateOrderError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/DuplicateOrder");

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith(CommonCodes.Duplicate, StringComparison.Ordinal) && e.Key == nameof(DataSeedStep.Order)));
  }

  [Test]
  public async Task Run_WhenNegativeOrderFound_ShouldThrowInvalidOrderError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/NegativeOrder");

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith(CommonCodes.Invalid, StringComparison.Ordinal) && e.Key == nameof(DataSeedStep.Order)));
  }

  [Test]
  public async Task Run_WhenUnmappedTypeFound_ShouldReturnUnmappedTypeError()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/UnmappedType");

    // Act
    var result = await runner.Run();

    // Assert
    Assert.That(result.Success, Is.False);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith("Unmapped", StringComparison.Ordinal) && e.Key == nameof(DataSeedStep.ItemType)));
  }

  [Test]
  public async Task Run_WithEnumInItems_ShouldRun()
  {
    // Arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/EnumType");

    // Act
    await runner.Run();

    // Assert
    Assert.That(runner.EnumPropModels, Has.Exactly(2).Items);
    Assert.That(runner.EnumPropModels[0].Number, Is.EqualTo(ExampleEnum.Value1));
    Assert.That(runner.EnumPropModels[1].Number, Is.EqualTo(ExampleEnum.Value2));
  }

  [Test]
  public async Task GetStepSummaries_WhenNoneRun_ShouldReturnAllPending()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");

    // act
    var summaries = await runner.GetStepSummaries();

    // assert
    Assert.That(summaries.Success, Is.True);
    Assert.That(summaries.Data, Has.Exactly(2).Items);
    Assert.That(summaries.Data[0].Order, Is.EqualTo(1));
    Assert.That(summaries.Data[0].Status, Is.EqualTo(DataSeedStepStatus.Pending));
    Assert.That(summaries.Data[1].Order, Is.EqualTo(2));
    Assert.That(summaries.Data[1].Status, Is.EqualTo(DataSeedStepStatus.Pending));
  }

  [Test]
  public async Task GetStepSummaries_WhenOneRun_ShouldReturnPendingAndCompleted()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "1"));

    // act
    var summaries = await runner.GetStepSummaries();

    // assert
    Assert.That(summaries.Success, Is.True);
    Assert.That(summaries.Data, Has.Exactly(2).Items);
    Assert.That(summaries.Data[0].Order, Is.EqualTo(1));
    Assert.That(summaries.Data[0].Status, Is.EqualTo(DataSeedStepStatus.Complete));
    Assert.That(summaries.Data[1].Order, Is.EqualTo(2));
    Assert.That(summaries.Data[1].Status, Is.EqualTo(DataSeedStepStatus.Pending));

  }

  [Test]
  public async Task GetStepSummaries_WhenOneRunWithDifferentHash_ShouldReturnHashMismatchStatus()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "not-1"));

    // act
    var summaries = await runner.GetStepSummaries();

    // assert
    Assert.That(summaries.Success, Is.True);
    Assert.That(summaries.Data, Has.Exactly(2).Items);
    Assert.That(summaries.Data[0].Order, Is.EqualTo(1));
    Assert.That(summaries.Data[0].Status, Is.EqualTo(DataSeedStepStatus.ValidationHashMismatch));
    Assert.That(summaries.Data[1].Order, Is.EqualTo(2));
    Assert.That(summaries.Data[1].Status, Is.EqualTo(DataSeedStepStatus.Pending));

  }

  [Test]
  public async Task GetStepSummaries_WhenOneRunWithNoFile_ShouldReturnMissingFileStatus()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(0, "step 0", DataSeedStepStatus.Complete, "not-1"));

    // act
    var summaries = await runner.GetStepSummaries();

    // assert
    Assert.That(summaries.Success, Is.True);
    Assert.That(summaries.Data, Has.Exactly(3).Items);
    Assert.That(summaries.Data[0].Order, Is.EqualTo(0));
    Assert.That(summaries.Data[0].Status, Is.EqualTo(DataSeedStepStatus.MissingInFile));
    Assert.That(summaries.Data[1].Order, Is.EqualTo(1));
    Assert.That(summaries.Data[1].Status, Is.EqualTo(DataSeedStepStatus.Pending));
    Assert.That(summaries.Data[2].Order, Is.EqualTo(2));
    Assert.That(summaries.Data[2].Status, Is.EqualTo(DataSeedStepStatus.Pending));

  }

  [Test]
  public async Task GetStepSummaries_ShouldReturnEmptyFileHashes()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "1"));

    // act
    var summaries = await runner.GetStepSummaries();

    // assert
    Assert.That(summaries.Success, Is.True);
    Assert.That(summaries.Data, Has.Exactly(2).Items);
    Assert.That(summaries.Data[0].ValidationHash, Is.EqualTo(string.Empty));
    Assert.That(summaries.Data[1].ValidationHash, Is.EqualTo(string.Empty));

  }

  [Test]
  public async Task ValidateSteps_WhenGivenValidSteps_ShouldReturnSuccessAndNotRunThem()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");

    // act
    var result = await runner.ValidateSteps(new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.True);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
  }

  [Test]
  public async Task ValidateSteps_WhenGivenInvalidSteps_ShouldReturnFailure()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/BadType");

    // act
    var result = await runner.ValidateSteps(new IgnoreDto(true, true));

    // assert
    Assert.That(result.Success, Is.False);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith(CommonCodes.NotFound, StringComparison.Ordinal) && e.Key == nameof(DataSeedStep.ItemType)));
  }

  [Test]
  public async Task ValidateStep_WhenGivenValidStepShouldReturnSuccessAndNotRunIt()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");

    // act
    var result = await runner.ValidateStep(1, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.True);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
  }

  [Test]
  public async Task ValidateStep_WhenGivenValidStep_ShouldReturnSuccessEvenIfOthersAreBad()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/SecondIsInvalidType");

    // act
    var result = await runner.ValidateStep(1, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.True);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
  }

  [Test]
  public async Task ValidateStep_WhenGivenInvalidStep_ShouldReturnFailure()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/SecondIsInvalidType");

    // act
    var result = await runner.ValidateStep(2, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.False);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
  }



  [Test]
  public async Task ValidateStep_WhenGivenIncorrectStepNumberStep_ShouldReturnNotFoundFailure()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");

    // act
    var result = await runner.ValidateStep(5, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.False);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith(CommonCodes.NotFound, StringComparison.Ordinal) && e.Key == nameof(DataSeedStep.Order)));
  }

  [Test]
  public async Task ValidateStep_WhenGivenHashMismatch_ShouldReturnFailure()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "not-1"));

    // act
    var result = await runner.ValidateStep(1, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.False);
  }

  [Test]
  public async Task ValidateStep_WhenGivenHashMismatch_ButIgnored_ShouldReturnSuccess()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "not-1"));

    // act
    var result = await runner.ValidateStep(1, new IgnoreDto(true, false));

    // assert
    Assert.That(result.Success, Is.True);
  }



  [Test]
  public async Task ValidateStep_WhenGivenOutOfOrder_ButIgnored_ShouldReturnSuccess()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(0, "step 0", DataSeedStepStatus.Complete, "not-1"));

    // act
    var result = await runner.ValidateStep(1, new IgnoreDto(true, false));

    // assert
    Assert.That(result.Success, Is.True);
  }


  [Test]
  public async Task ValidateStep_WhenGivenNonBadFile_ShouldReturnFailure()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/EmptyFile");

    // act
    var result = await runner.ValidateStep(1, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.False);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
    Assert.That(result.Errors, Is.Not.Empty);
  }

  [Test]
  public async Task RunStep_WhenGivenValid_ShouldRun()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");

    // act
    var result = await runner.RunStep(1, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.True);
    Assert.That(repo.Steps, Has.Exactly(1).Items);
  }

  [Test]
  public async Task RunStep_WhenGivenNonExistentOrder_ShouldReturnFailureNotFound()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/Pass");

    // act
    var result = await runner.RunStep(5, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.False);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith(CommonCodes.NotFound, StringComparison.Ordinal) && e.Key == nameof(DataSeedStep.Order)));
  }


  [Test]
  public async Task RunStep_WhenGivenNonBadFile_ShouldReturnFailure()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/EmptyFile");

    // act
    var result = await runner.RunStep(1, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.False);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
    Assert.That(result.Errors, Is.Not.Empty);
  }



  [Test]
  public async Task RunStep_WhenGivenAllReadyRun_ShouldReturnFailure()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var mockHashGenerator = new Mock<IFileHashGenerator>();
    mockHashGenerator.Setup(x => x.GenerateHash(It.IsAny<string>())).Returns((string f) => f.Contains("\"order\": 1") ? "1" : "2");
    var runner = new TestSeedRunner(repo, mockHashGenerator.Object);
    runner.SetFolder("TestDataSeed/Pass");
    await repo.SaveDataSeedStep(new DataSeedStepDto(1, "step 1", DataSeedStepStatus.Complete, "1"));

    // act
    var result = await runner.RunStep(1, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.False);
    Assert.That(repo.Steps, Has.Exactly(1).Items);
    Assert.That(result.Errors, Has.One.Matches<KeyCode>(e => e.Code.StartsWith("AlreadyRun", StringComparison.Ordinal) && e.Key == nameof(DataSeedStep.Order)));
  }


  [Test]
  public async Task RunStep_WhenHasValidationError_ShouldReturnFailureSuccess()
  {
    // arrange
    var repo = new TestDataSeedRepo();
    var runner = new TestSeedRunner(repo, new SHA256HashGenerator());
    runner.SetFolder("TestDataSeed/UnmappedType");

    // act
    var result = await runner.RunStep(1, new IgnoreDto(false, false));

    // assert
    Assert.That(result.Success, Is.False);
    Assert.That(repo.Steps, Has.Exactly(0).Items);
  }


}
