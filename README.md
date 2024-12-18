# VectorCode.DataSeed

Data Seed Core


## ItemType

You may need to speficy the assembly name, for example  "VectorCode.DataSeed.Test.TestDataSeed.StepOneModel, VectorCode.DataSeed.Test"

## Data Seed Test Folder Paths

These should use the operating system agnostics folder seperator `/` and not `\`.

## Usage

There are two main ways to use this data seed library, in the startup of the application calling 'run' which will try to run all the data seed steps in the order they are defined in the configuration file. The other way is to call 'runStep' which will run a single step and could be called by an admin console.

## Future Enhacements
- Timeout for each step
- Rollbacks
- Override hash check