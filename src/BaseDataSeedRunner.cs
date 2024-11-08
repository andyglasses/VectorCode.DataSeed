using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodedVector.DataSeed;

/// <summary>
/// Base class for all Data Seed Running
/// </summary>
public abstract class BaseDataSeedRunner
{
  private readonly IDataSeedRepository _dataSeedRepository;

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
  public BaseDataSeedRunner(IDataSeedRepository dataSeedRepository)
  {
    _dataSeedRepository = dataSeedRepository;
  }

  /// <summary>
  /// Run the data seeding process
  /// </summary>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException">When a data seed fails to run</exception>
  public async Task Run()
  {
    var files = Directory.GetFiles(Folder, "*.json");

    var steps = new List<(DataSeedStep Step, Type? Type, string ValidationHash)>();
    var options = new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true
    };
    files.ToList().ForEach(f => ProcessFile(steps, options, f));
    var repoSteps = await _dataSeedRepository.GetDataSeedSteps();

    // validate steps
    ValidateSteps(steps, repoSteps!);

    var orderedSteps = steps.OrderBy(x => x.Step.Order).ToList();
    foreach (var step in orderedSteps)
    {
      if(repoSteps.Any(x => x.Order == step.Step.Order && x.Status == DataSeedStepStatus.Complete))
      {
        continue;
      }
      await ProcessStep(options, step);
    }

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

  private void ValidateSteps(List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, List<DataSeedStepDto> repoSteps)
  {
    var duplicates = steps.GroupBy(x => x.Step.Order).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
    if (duplicates.Count > 0)
    {
      throw new InvalidOperationException($"Duplicate data seed step order found ({string.Join(',', duplicates)}).");
    }

    if (steps.Any(x => x.Step.Order < 0))
    {
      throw new InvalidOperationException("Data seed step order must be greater than 0.");
    }

    var unknownTypes = steps.Where(s => s.Type == null).Select(s => s.Step.ItemType).ToList();
    if (unknownTypes.Count > 0)
    {
      throw new InvalidOperationException($"Unknown data seed step types ({string.Join(',', unknownTypes)}).");
    }

    var unmappedTypes = steps.Where(s => GetStep(s.Type!) == null).Select(s => s.Step.ItemType).ToList();
    if (unmappedTypes.Count > 0)
    {
      throw new InvalidOperationException($"Unmapped data seed step types ({string.Join(',', unmappedTypes)}), make sure they are mapped in GetStep.");
    }

    var repoHashes = repoSteps.ToDictionary(x => x.Order, x => x.ValidationHash);
    var mismatchedSteps = steps.Where(x => repoHashes.ContainsKey(x.Step.Order) && repoHashes[x.Step.Order] != x.ValidationHash).ToList();
    if (mismatchedSteps.Count > 0)
    {
      throw new InvalidOperationException($"Data seed steps have changed since last run ({string.Join(',', mismatchedSteps.Select(x => x.Step.Order))}).");
    }

    var maxRunStep = repoSteps.Where(s => s.Status == DataSeedStepStatus.Complete).Select(x => x.Order).DefaultIfEmpty(0).Max();
    var unrunSteps = steps.Where(x =>! repoSteps.Any(r => r.Order == x.Step.Order && r.Status == DataSeedStepStatus.Complete)).ToList();

    if(unrunSteps.Any(x => x.Step.Order <= maxRunStep))
    {
      throw new InvalidOperationException($"Data seed steps must be run in order.");
    }
  }

  private static void ProcessFile(List<(DataSeedStep Step, Type? Type, string ValidationHash)> steps, JsonSerializerOptions options, string file)
  {
    var f = File.ReadAllText(file);
    if (string.IsNullOrWhiteSpace(f)) 
    {
      throw new InvalidOperationException($"Failed to parse data seed step {file} (empty file)");
    }

    DataSeedStep? step = null;
    try
    {
      step = JsonSerializer.Deserialize<DataSeedStep>(f, options);
    }
    catch (JsonException ex)
    {
      throw new InvalidOperationException($"Failed to parse data seed step {file}", ex);
    }
    if (step == null) 
    { 
      throw new InvalidOperationException($"Failed to parse data seed step {file} (null result)"); 
    }

    var hashbytes = SHA256.HashData(Encoding.UTF8.GetBytes(f));
    StringBuilder sb = new StringBuilder();
    foreach (byte b in hashbytes)
    {
      sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
    }
    var hash = sb.ToString();
    steps.Add(new(step, Type.GetType(step.ItemType), hash));
  }
}
