using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodedVector.DataSeed;

/// <summary>
/// Model for holding basic data about a data sed step
/// </summary>
/// <param name="Order">Execusion order of the step</param>
/// <param name="Name">Name of the step</param>
/// <param name="Status">The status of the step</param>
/// <param name="ValidationHash">Hash of the data to validate the step</param>
public record DataSeedStepDto(int Order, string Name, DataSeedStepStatus Status, string ValidationHash);
