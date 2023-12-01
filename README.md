# Running Azurite in CI

This repository has samples of how you can run [Azurite](https://github.com/Azure/Azurite), the Azure Storage emulator, in your continuous integration (CI) build.

See [`tests/StorageTests.cs`](tests/StorageTests.cs) for a sample of how to use Azurite in some .NET tests. The sample uses C# and xUnit.

The CI services with samples are listed below:

| CI service      | Status                                                                                                                                                                                 | Sample                                                             |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Azure Pipelines | [![Azure Pipelines](https://dev.azure.com/joelverhagen/oss/_apis/build/status%2Fjoelverhagen.azurite-sample)](https://dev.azure.com/joelverhagen/oss/_build?definitionId=1&_a=summary) | [`.pipelines/test.yml`](.pipelines/test.yml#L32-L40)               |
| GitHub Actions  | [![GitHub Actions](https://github.com/joelverhagen/azurite-sample/actions/workflows/test.yml/badge.svg)](https://github.com/joelverhagen/azurite-sample/actions/workflows/test.yml)    | [`.github/workflows/test.yml`](.github/workflows/test.yml#L37-L46) |

