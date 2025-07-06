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

1. Change the version in the .csproj file (in the `<Version>` property).

1. Run the following command to push the package to Nuget.

    ```bash
    ./nuget-push.sh <version> <api_key>
    ```

## Nuget Package

You can find the latest version of the package [here](https://www.nuget.org/packages/XiansAi.Lib/).
