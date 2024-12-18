using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VectorCode.Common;

namespace VectorCode.DataSeed;

/// <summary>
/// Base class for all Data Seed Running
/// </summary>
public abstract class BaseDataSeedRunner
{
  private readonly IDataSeedRepository _dataSeedRepository;
  private readonly IFileHashGenerator _fileHashGenerator;

  /// <summary>
  /// Data seed folder
  /// </summary>
  protected abstract string Folder { get; }

  /// <summary>
  /// Gets the task runner for the item type
  /// </summary>
  /// <param name="itemType"></param>
  /// <returns></returns>
  public abstract Func<dynamic, Task>? GetStep(Type itemType);

  /// <summary>
  /// Create a new instance of the data seed runner
  /// </summary>
  /// <param name="dataSeedRepository">Repository instance to log data seed executions</param>
  /// <param name="fileHashGenerator"></param>
  public BaseDataSeedRunner(IDataSeedRepository dataSeedRepository, IFileHashGenerator fileHashGenerator)
  {
    _dataSeedRepository = dataSeedRepository;
    _fileHashGenerator = fileHashGenerator;
  }

  /// <summary>
  /// Run the data seeding process
  /// </summary>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException">When a data seed fails to run</exception>
  public async Task<Response<List<DataSeedStepDto>>> Run()
  {
    var steps = GetFiles();
    if (!steps.Success)
    {
      return Response.Builder.Failed<List<DataSeedStepDto>>(steps.Errors);
    }
    var repoSteps = await _dataSeedRepository.GetDataSeedSteps();

    // validate steps
    var validateResponse = ValidateSteps(steps.Data!, repoSteps!, new IgnoreDto(false, false));
    if (!validateResponse.Success)
    {
      return Response.Builder.Failed<List<DataSeedStepDto>>(validateResponse.Errors);
    }

    var orderedSteps = steps.Data!.OrderBy(x => x.Step.Order).ToList();
    var stepsRun = new List<DataSeedStep>();
    foreach (var step in orderedSteps)
    {
      if(repoSteps.Any(x => x.Order == step.Step.Order && x.Status == DataSeedStepStatus.Complete))
      {
        continue;
      }
      await ProcessStep(GetJsonSerializerOptions(), step);
      stepsRun.Add(step.Step);
    }

    return Response.Builder.Successful(stepsRun.Select(x => new DataSeedStepDto(x.Order, x.Name, DataSeedStepStatus.Complete, string.Empty)).ToList());

  }

  /// <summary>
  /// Returns all the migrations
  /// </summary>
  /// <returns></returns>
  public async Task<Response<List<DataSeedStepDto>>> GetStepSummaries()
  {
    var steps = GetFiles();

    if (!steps.Success)
    {
      return Response.Builder.Failed<List<DataSeedStepDto>>(steps.Errors);
    }

    var repoSteps = await _dataSeedRepository.GetDataSeedSteps();

    var fileDtoSteps = steps.Data!.Select(x => new DataSeedStepDto(
      x.Step.Order, 
      x.Step.Name, 
      repoSteps.Any(s => s.Order == x.Step.Order) ? 
        (x.ValidationHash != repoSteps.FirstOrDefault(r => r.Order == x.Step.Order)!.ValidationHash ? DataSeedStepStatus.ValidationHashMismatch : 
        DataSeedStepStatus.Complete) : DataSeedStepStatus.Pending, 
      x.ValidationHash)).ToList();

    var repoDtoSteps = repoSteps.Where(x => !fileDtoSteps.Any(f => f.Order == x.Order)).Select(x => new DataSeedStepDto(x.Order, x.Name, DataSeedStepStatus.MissingInFile, x.ValidationHash)).ToList();
    var results = fileDtoSteps.Concat(repoDtoSteps).OrderBy(x => x.Order).Select(r => r with { ValidationHash = "" }).ToList();
    return Response.Builder.Successful(results);
  }

  /// <summary>
  /// Valdiates the steps
  /// </summary>
  /// <param name="ignoreSettings">Settings to turn off some validations</param>
  /// <returns></returns>
  public async Task<Response> ValidateSteps(IgnoreDto ignoreSettings)
  {
    var steps = GetFiles();
    if (!steps.Success)
    {
      return Response.Builder.Failed(steps.Errors);
    }
    var repoSteps = await _dataSeedRepository.GetDataSeedSteps();

    // validate steps
    return ValidateSteps(steps.Data!, repoSteps!, ignoreSettings);
  }

  /// <summary>
  /// Valdiates a step
  /// </summary>
  /// <param name="order">The order of the step to validate</param>
  /// <param name="ignoreSettings">Settings to turn off some validations</param>
  /// <returns></returns>
  public async Task<Response> ValidateStep(int order, IgnoreDto ignoreSettings)
  {
    var steps = GetFiles();
    if (!steps.Success)
    {
      return Response.Builder.Failed(steps.Errors);
    }
    if (!steps.Data!.Any(s => s.Step.Order == order))
    {
      return Response.Builder.Failed(new List<KeyCode> { new(nameof(DataSeedStep.Order), CommonCodes.NotFound) });
    }
    var repoSteps = await _dataSeedRepository.GetDataSeedSteps();

    // validate steps
    return ValidateSteps(steps.Data!, repoSteps!, ignoreSettings, order);
  }

  /// <summary>
  /// Run a step
  /// </summary>
  /// <param name="order">The step order to run</param>
  /// <param name="ignoreSettings">Settings to ignore</param>
  /// <returns></returns>
  public async Task<Response> RunStep(int order, IgnoreDto ignoreSettings)
  {
    var steps = GetFiles();

    if (!steps.Success)
    {
      return Response.Builder.Failed(steps.Errors);
    }

    if (!steps.Data!.Any(s => s.Step.Order == order))
    {
      return Response.Builder.Failed(new List<KeyCode> { new(nameof(DataSeedStep.Order), CommonCodes.NotFound) });
    }
    var repoSteps = await _dataSeedRepository.GetDataSeedSteps();

    if(repoSteps.Any(s => s.Status == DataSeedStepStatus.Complete && s.Order == order))
    {
      return Response.Builder.Failed(new List<KeyCode> { new(nameof(DataSeedStep.Order), "AlreadyRun") });
    }

    // validate steps
    var validateResponse = ValidateSteps(steps.Data!, repoSteps!, ignoreSettings, order);

    if(!validateResponse.Success)
    {
      return validateResponse;
    }

    var step = steps.Data!.First(s => s.Step.Order == order);

    await ProcessStep(GetJsonSerializerOptions(), step);

    return Response.Builder.Successful();
  }


  private Response<List<(DataSeedStep Step, Type? Type, string ValidationHash)>> GetFiles()
  {
    var returnVal = new Response<List<(DataSeedStep Step, Type? Type, string ValidationHash)>>();
    var files = Directory.GetFiles(Folder, "*.json");

    var steps = new List<(DataSeedStep Step, Type? Type, string ValidationHash)>();
    
    files.ToList().ForEach(f =>
    {
      var response = ProcessFile(GetJsonSerializerOptions(), f);
      if (response.Success)
      {
        steps.Add(response.Data);
      }
      else
      {
        returnVal.Errors.AddRange(response.Errors);
      }
    });
  
    if(returnVal.Errors.Count == 0)
    {
      returnVal.Success = true;
      returnVal.Data = steps;
    }
    return returnVal;
  }

  private static JsonSerializerOptions GetJsonSerializerOptions()
  {
    return new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true,
      Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
  }

  private async Task ProcessStep(JsonSerializerOptions options, (DataSeedStep Step, Type? Type, string ValidationHash) step)
  {
    var stepRunner = GetStep(step.Type!)!;
    for (int i = 0; i < step.Step.Items.Count; i++)
    {
      dynamic? item = step.Step.Items[i];
      var itemJson = JsonSerializer.Serialize(item);
      var typedItem = JsonSerializer.Deserialize(itemJson, step.Type, options);
      if (typedItem == null) 
      { 
        throw new InvalidOperationException($"Failed to parse item {i} to {step.Step.ItemType}");
      }
      await stepRunner(typedItem);
    }

    await _dataSeedRepository.SaveDataSeedStep(new DataSeedStepDto(step.Step.Order, step.Step.Name, DataSeedStepStatus.Complete, step.ValidationHash));
  }

  private Response ValidateSteps(
    List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, 
    List<DataSeedStepDto> repoSteps, 
    IgnoreDto ignoreSettings,
    int? stepToValidate = null)
  {
    var returnVal = new Response();
    ValidateStepsForDuplicates(steps, stepToValidate, returnVal);
    ValidateStepsForNegative(steps, stepToValidate, returnVal);
    ValidateItemTypeForNotFoundTypes(steps, stepToValidate, returnVal);
    ValidateItemTypeForUnmappedTypes(steps, stepToValidate, returnVal);

    if (!ignoreSettings.HashMismatch)
    {
      ValidateHashMismatches(steps, repoSteps, stepToValidate, returnVal);
    }

    if (!ignoreSettings.OutOfOrder)
    {
      ValidateRunOutofOrder(steps, repoSteps, stepToValidate, returnVal);
    }

    if (returnVal.Errors.Count == 0)
    {
      returnVal.Success = true;
    }

    return returnVal;
  }

  private static void ValidateRunOutofOrder(List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, List<DataSeedStepDto> repoSteps, int? stepToValidate, Response returnVal)
  {
    var maxRunStep = repoSteps.Where(s => s.Status == DataSeedStepStatus.Complete).Select(x => x.Order).DefaultIfEmpty(0).Max();
    var unrunSteps = steps.Where(x => !repoSteps.Any(r => r.Order == x.Step.Order && r.Status == DataSeedStepStatus.Complete) && (!stepToValidate.HasValue || x.Step.Order == stepToValidate.Value)).ToList();

    if (unrunSteps.Any(x => x.Step.Order <= maxRunStep))
    {
      returnVal.Success = false;
      returnVal.Errors.Add(new KeyCode(nameof(DataSeedStep.Order), "OutOfOrder"));
    }
  }

  private static void ValidateHashMismatches(List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, List<DataSeedStepDto> repoSteps, int? stepToValidate, Response returnVal)
  {
    var repoHashes = repoSteps.ToDictionary(x => x.Order, x => x.ValidationHash);
    var mismatchedSteps = steps.Where(x => repoHashes.ContainsKey(x.Step.Order) && repoHashes[x.Step.Order] != x.ValidationHash).ToList();
    if (mismatchedSteps.Count > 0 && (!stepToValidate.HasValue || mismatchedSteps.Any(a => a.Step.Order == stepToValidate.Value)))
    {
      returnVal.Success = false;
      returnVal.Errors.Add(
        KeyCode.Builder.KeyCodeWithStringListDetail(
          nameof(DataSeedStepDto.ValidationHash),
          "Mismatch",
          mismatchedSteps.Select(s => s.Step.Order.ToString(CultureInfo.InvariantCulture)).ToList()));
    }
  }

  private void ValidateItemTypeForUnmappedTypes(List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, int? stepToValidate, Response returnVal)
  {
    var unmappedTypes = steps.Where(s => GetStep(s.Type!) == null).ToList();
    if (unmappedTypes.Count > 0 && (!stepToValidate.HasValue || unmappedTypes.Any(a => a.Step.Order == stepToValidate.Value)))
    {
      returnVal.Success = false;
      returnVal.Errors.Add(
        KeyCode.Builder.KeyCodeWithStringListDetail(
          nameof(DataSeedStep.ItemType),
          "Unmapped",
          unmappedTypes.Select(t => $"{t.Step.Order}-{t.Step.ItemType}").ToList()));
    }
  }

  private static void ValidateItemTypeForNotFoundTypes(List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, int? stepToValidate, Response returnVal)
  {
    var unknownTypes = steps.Where(s => s.Type == null).ToList();
    if (unknownTypes.Count > 0 && (!stepToValidate.HasValue || unknownTypes.Any(a => a.Step.Order == stepToValidate.Value)))
    {
      returnVal.Success = false;
      returnVal.Errors.Add(
        KeyCode.Builder.KeyCodeWithStringListDetail(
          nameof(DataSeedStep.ItemType),
          CommonCodes.NotFound,
          unknownTypes.Select(t => $"{t.Step.Order}-{t.Step.ItemType}").ToList()));
    }
  }

  private static void ValidateStepsForNegative(List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, int? stepToValidate, Response returnVal)
  {
    var negativeSteps = steps.Where(x => x.Step.Order < 0).ToList();
    if (negativeSteps.Count > 0 && (!stepToValidate.HasValue || negativeSteps.Any(a => a.Step.Order == stepToValidate.Value)))
    {
      returnVal.Success = false;
      returnVal.Errors.Add(
        KeyCode.Builder.KeyCodeWithStringListDetail(
          nameof(DataSeedStep.Order),
          CommonCodes.Invalid,
          negativeSteps.Select(d => d.Step.Order.ToString(CultureInfo.InvariantCulture)).ToList()));
    }
  }

  private static void ValidateStepsForDuplicates(List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, int? stepToValidate, Response returnVal)
  {
    var duplicates = steps.GroupBy(x => x.Step.Order).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
    if (duplicates.Count > 0 && (!stepToValidate.HasValue || duplicates.Contains(stepToValidate.Value)))
    {
      returnVal.Success = false;
      returnVal.Errors.Add(
        KeyCode.Builder.KeyCodeWithStringListDetail(
          nameof(DataSeedStep.Order),
          CommonCodes.Duplicate,
          duplicates.Select(d => d.ToString(CultureInfo.InvariantCulture)).ToList()));
    }
  }

  private Response<(DataSeedStep Step, Type? Type, string ValidationHash)> ProcessFile(JsonSerializerOptions options, string file)
  {
    var f = File.ReadAllText(file);
    if (string.IsNullOrWhiteSpace(f)) 
    {
      return Response.Builder.Failed<(DataSeedStep Step, Type? Type, string ValidationHash)>(new List<KeyCode> { new(file, "EmptyFile") });
    }

    DataSeedStep? step = null;
    try
    {
      step = JsonSerializer.Deserialize<DataSeedStep>(f, options);
    }
    catch (JsonException ex)
    {
      return Response.Builder.Failed<(DataSeedStep Step, Type? Type, string ValidationHash)>(new List<KeyCode> { KeyCode.Builder.KeyCodeWithStringDetail(file, "FailedToParse", ex.Message.Replace(":", "=")) });
    }
    if (step == null)
    {
      return Response.Builder.Failed<(DataSeedStep Step, Type? Type, string ValidationHash)>(new List<KeyCode> { new(file, "NullResult") });
    }

    var hash = _fileHashGenerator.GenerateHash(f);
    var type = Type.GetType(step.ItemType);
    return Response.Builder.Successful<(DataSeedStep Step, Type? Type, string ValidationHash)>(new(step, type, hash));
  }
}
