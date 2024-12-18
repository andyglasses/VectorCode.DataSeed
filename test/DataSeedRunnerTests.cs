// Ignore Spelling: Json

using VectorCode.DataSeed.Test.TestDataSeed;
using FluentAssertions;
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code == "OutOfOrder" && e.Key == nameof(DataSeedStepDto.Order));
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith("Mismatch") && e.Key == nameof(DataSeedStepDto.ValidationHash));
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith("FailedToParse:") && e.Key.EndsWith("001.json"));
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith("FailedToParse:") && e.Key.EndsWith("001.json"));
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code == "EmptyFile" && e.Key.EndsWith("001.json"));
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith(CommonCodes.NotFound) && e.Key == nameof(DataSeedStep.ItemType));
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith(CommonCodes.Duplicate) && e.Key == nameof(DataSeedStep.Order));
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith(CommonCodes.Invalid) && e.Key == nameof(DataSeedStep.Order));
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith("Unmapped") && e.Key == nameof(DataSeedStep.ItemType));
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
    runner.EnumPropModels.Should().HaveCount(2);
    runner.EnumPropModels[0].Number.Should().Be(ExampleEnum.Value1);
    runner.EnumPropModels[1].Number.Should().Be(ExampleEnum.Value2);
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
    summaries.Success.Should().BeTrue();
    summaries.Data.Should().HaveCount(2);
    summaries.Data![0].Order.Should().Be(1);
    summaries.Data![0].Status.Should().Be(DataSeedStepStatus.Pending);
    summaries.Data![1].Order.Should().Be(2);
    summaries.Data![1].Status.Should().Be(DataSeedStepStatus.Pending);
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
    summaries.Success.Should().BeTrue();
    summaries.Data.Should().HaveCount(2);
    summaries.Data![0].Order.Should().Be(1);
    summaries.Data![0].Status.Should().Be(DataSeedStepStatus.Complete);
    summaries.Data![1].Order.Should().Be(2);
    summaries.Data![1].Status.Should().Be(DataSeedStepStatus.Pending);

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
    summaries.Success.Should().BeTrue();
    summaries.Data.Should().HaveCount(2);
    summaries.Data![0].Order.Should().Be(1);
    summaries.Data![0].Status.Should().Be(DataSeedStepStatus.ValidationHashMismatch);
    summaries.Data![1].Order.Should().Be(2);
    summaries.Data![1].Status.Should().Be(DataSeedStepStatus.Pending);

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
    summaries.Success.Should().BeTrue();
    summaries.Data.Should().HaveCount(3);
    summaries.Data![0].Order.Should().Be(0);
    summaries.Data![0].Status.Should().Be(DataSeedStepStatus.MissingInFile);
    summaries.Data![1].Order.Should().Be(1);
    summaries.Data![1].Status.Should().Be(DataSeedStepStatus.Pending);
    summaries.Data![2].Order.Should().Be(2);
    summaries.Data![2].Status.Should().Be(DataSeedStepStatus.Pending);

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
    summaries.Success.Should().BeTrue();
    summaries.Data.Should().HaveCount(2);
    summaries.Data![0].ValidationHash.Should().Be(string.Empty);
    summaries.Data![1].ValidationHash.Should().Be(string.Empty);

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
    result.Success.Should().BeTrue();
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith(CommonCodes.NotFound) && e.Key == nameof(DataSeedStep.ItemType));
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeTrue();
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeTrue();
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeFalse();
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith(CommonCodes.NotFound) && e.Key == nameof(DataSeedStep.Order));
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeFalse();
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
    result.Success.Should().BeTrue();
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
    result.Success.Should().BeTrue();
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
    result.Success.Should().BeFalse();
    result.Errors.Should().HaveCountGreaterThan(0);
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeTrue();
    repo.Steps.Should().HaveCount(1);
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith(CommonCodes.NotFound) && e.Key == nameof(DataSeedStep.Order));
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeFalse();
    result.Errors.Should().HaveCountGreaterThan(0);
    repo.Steps.Should().HaveCount(0);
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
    result.Success.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Code.StartsWith("AlreadyRun") && e.Key == nameof(DataSeedStep.Order));
    repo.Steps.Should().HaveCount(1);
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
    result.Success.Should().BeFalse();
    repo.Steps.Should().HaveCount(0);
  }


}
