# Xians.Ai Lib

This is a library for the Xians.Ai flow projects. This LIbrary is used by the Flows. It contains the base classes, interfaces and other utilities for creating and running Flows.

## Installation

```bash
git clone https://github.com/XiansAiPlatform/XiansAi.Lib.git
touch .env
dotnet build
```

.env file is required to run tests. It contains the environment variables for the tests.

## Publishing to Nuget

Ensure you update the version in the nuspec file and use the correctversion in the command below.

```bash

dotnet clean
dotnet build -c Release

mkdir nupkg
dotnet pack -c Release -o ./nupkg
dotnet nuget push ./nupkg/XiansAi.Lib.1.0.1.nupkg -s https://api.nuget.org/v3/index.json -k <your-api-key>
```

## Nuget Package

You can find the latest version of the package [here](https://www.nuget.org/packages/XiansAi.Lib/).

