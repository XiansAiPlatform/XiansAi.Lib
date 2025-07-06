# Xians.Ai Lib

This is a library for the Xians.Ai flow projects. This LIbrary is used by the Flows. It contains the base classes, interfaces and other utilities for creating and running Flows.

## Installation

```bash
git clone https://github.com/XiansAiPlatform/XiansAi.Lib.git
touch .env
dotnet build
```

.env file is required to run tests. It contains the environment variables for the tests.

## Publishing to NuGet

### Automated Publishing (Recommended)

The easiest way to publish a new version is through GitHub Actions automation:

```bash
# Create and push a version tag
git tag -a v1.3.6 -m "Release v1.3.6"
git push origin v1.3.6
```

This automatically builds, tests, and publishes the package to NuGet.org.

### Manual Publishing (Alternative)

For manual publishing, use the provided script:

```bash
./nuget-push.sh <version> <api_key>
```

### 📚 Detailed Documentation

For comprehensive publishing instructions, troubleshooting, and package consumption guides, see: **[NuGet Documentation](docs/NUGET.md)**

## NuGet Package

You can find the latest version of the package [here](https://www.nuget.org/packages/XiansAi.Lib/).

### Installation

```bash
dotnet add package XiansAi.Lib --version 1.3.6
```

Or add to your `.csproj`:

```xml
<PackageReference Include="XiansAi.Lib" Version="1.3.6" />
```
