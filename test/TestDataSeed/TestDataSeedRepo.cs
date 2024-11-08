using System.Collections.Immutable;

namespace CodedVector.DataSeed.Test.TestDataSeed;

public class TestDataSeedRepo : IDataSeedRepository
{
  private readonly Dictionary<int, DataSeedStepDto> _steps = new Dictionary<int, DataSeedStepDto>();
  public ImmutableDictionary<int, DataSeedStepDto> Steps => _steps.ToImmutableDictionary();
  public Task<List<DataSeedStepDto>> GetDataSeedSteps()
  {
    return Task.FromResult(_steps.Values.ToList());
  }

  public Task SaveDataSeedStep(DataSeedStepDto stepDto)
  {
    _steps[stepDto.Order] = stepDto;
    return Task.CompletedTask;
  }
}
