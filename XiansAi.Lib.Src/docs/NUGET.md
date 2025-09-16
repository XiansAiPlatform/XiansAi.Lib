# NuGet Package Documentation

This document provides comprehensive instructions for building, publishing, and consuming the Agentri.SDK NuGet package.

## Table of Contents

1. [Automated Publishing via GitHub Actions](#automated-publishing-via-github-actions)
2. [Manual Publishing NuGet Packages (not recommended)](#manual-publishing-nuget-packages-not-recommended)
3. [Consuming Published NuGet Packages](#consuming-published-nuget-packages)
4. [Building and Testing Packages Locally](#building-and-testing-packages-locally)
5. [Troubleshooting](#troubleshooting)

---

## Automated Publishing via GitHub Actions

### Overview

The repository includes GitHub Actions automation that automatically builds and publishes NuGet packages when you create version tags.

### Quick Start

Change the version in the XiansAi.Lib.csproj file.

```xml
<Version>1.3.7</Version>
```

```bash
# Define the version
export VERSION=1.3.7 # or 1.3.7-beta for pre-release

# Create and push a version tag
git tag -a v$VERSION -m "Release v$VERSION"
git push origin v$VERSION
```

### Delete existing tag (optional)

```bash
git tag -d v$VERSION
git push origin :refs/tags/v$VERSION
```

### What Gets Published

The automation publishes to: **NuGet.org** with package ID `XiansAi.Lib`

**Package Information:**

- **Package ID**: `XiansAi.Lib`
- **Target Framework**: `.NET 9.0`
- **Authors**: `99x`
- **Repository**: `https://github.com/XiansAiPlatform/XiansAi.Lib`

### Version Tag Examples

**Stable releases:**

- `v1.3.6` → Package version `1.3.6`
- `v2.0.0` → Package version `2.0.0`

**Pre-releases:**

- `v1.3.6-beta` → Package version `1.3.6-beta`
- `v2.0.0-alpha` → Package version `2.0.0-alpha`
- `v1.3.6-rc1` → Package version `1.3.6-rc1`

### Workflow Features

- **Automatic Version Extraction**: Strips 'v' prefix from tags
- **Version Validation**: Ensures semantic versioning format
- **Dependency Restoration**: Restores NuGet dependencies
- **Testing**: Runs test suite before publishing
- **Duplicate Protection**: Uses `--skip-duplicate` flag
- **Build Summary**: Generates detailed publish summary

### Monitoring Builds

1. Go to the repository's **Actions** tab on GitHub
2. Look for "Build and Publish to NuGet" workflows
3. Check [NuGet.org](https://www.nuget.org/packages/XiansAi.Lib) for newly published packages

---

## Manual Publishing NuGet Packages (not recommended)

### Prerequisites

- .NET 9.0 SDK installed
- NuGet API key from [NuGet.org](https://www.nuget.org/)
- Access to the repository

### Step-by-Step Instructions

1. **Set up your environment:**

   ```bash
   cd XiansAi.Lib.Src
   export VERSION=1.3.6
   export NUGET_API_KEY=your-api-key-here
   ```

2. **Run the manual publish script:**

   ```bash
   ./nuget-push.sh $VERSION $NUGET_API_KEY
   ```

### What the Script Does

The `nuget-push.sh` script performs the following actions:

- **Validates input parameters** - Ensures version and API key are provided
- **Cleans the solution** - Removes previous build artifacts
- **Builds in Release mode** - Compiles the project for production
- **Creates packages** - Generates .nupkg files with specified version
- **Pushes to NuGet** - Uploads the package to NuGet.org

### Script Parameters

| Parameter | Description | Example | Required |
|-----------|-------------|---------|----------|
| `VERSION` | Package version to publish | `1.3.6` | Yes |
| `API_KEY` | NuGet API key from NuGet.org | `oy2...` | Yes |

---

## Consuming Published NuGet Packages

### Package Manager UI (Visual Studio)

1. Right-click on your project in Solution Explorer
2. Select "Manage NuGet Packages"
3. Go to the "Browse" tab
4. Search for `XiansAi.Lib`
5. Select the desired version and click "Install"

### Package Manager Console

```powershell
Install-Package XiansAi.Lib -Version 1.3.6
```

### .NET CLI

```bash
dotnet add package XiansAi.Lib --version 1.3.6
```

### PackageReference (csproj)

Add this to your `.csproj` file:

```xml
<PackageReference Include="XiansAi.Lib" Version="1.3.6" />
```

### Using in Code

```csharp
using Agentri.SDK;
using Agentri.Flow;
using Agentri.Knowledge;

// Example usage
var agent = new Agent();
var flow = new Flow();
var knowledgeHub = new KnowledgeHub();
```

### Version Specifications

| Version Pattern | Description | Example |
|----------------|-------------|---------|
| `1.3.6` | Exact version | `1.3.6` |
| `1.3.*` | Latest patch version | `1.3.6`, `1.3.7`, etc. |
| `1.*` | Latest minor version | `1.3.6`, `1.4.0`, etc. |
| `[1.3.6,)` | Minimum version | `1.3.6` or higher |
| `[1.3.6,1.4.0)` | Version range | `1.3.6` to `1.4.0` (exclusive) |

---

## Building and Testing Packages Locally

### For Development and Testing

1. **Build the project:**

   ```bash
   cd XiansAi.Lib.Src
   dotnet clean
   dotnet build -c Release
   ```

2. **Run tests:**

   ```bash
   cd ../XiansAi.Lib.Tests
   dotnet test -c Release
   ```

3. **Create local package:**

   ```bash
   cd ../XiansAi.Lib.Src
   dotnet pack -c Release -o ./nupkg /p:Version=1.3.6-local
   ```

### Local Package Testing

1. **Add local package source:**

   ```bash
   dotnet nuget add source ./nupkg --name "Local"
   ```

2. **Create test project:**

   ```bash
   mkdir TestApp
   cd TestApp
   dotnet new console
   dotnet add package XiansAi.Lib --version 1.3.6-local --source Local
   ```

3. **Test the package:**

   ```csharp
   using Agentri.SDK;
   
   Console.WriteLine("Testing XiansAi.Lib package");
   ```

### Package Inspection

```bash
# List package contents
dotnet nuget list source

# View package metadata
nuget spec XiansAi.Lib

# Extract and inspect package
unzip -l ./nupkg/XiansAi.Lib.1.3.6.nupkg
```

---

## Troubleshooting

### Common Issues

1. **API Key Authentication Error:**

   ```bash
   # Verify API key is valid
   dotnet nuget push --help
   
   # Check API key permissions on NuGet.org
   ```

2. **Version Already Exists:**

   ```bash
   # Error: Package version already exists
   # Solution: Increment version number or use pre-release suffix
   ```

3. **Build Errors:**

   ```bash
   # Clean and restore
   dotnet clean
   dotnet restore
   dotnet build -c Release
   ```

4. **Test Failures:**

   ```bash
   # Run tests with detailed output
   dotnet test -c Release --verbosity detailed
   
   # Run specific test
   dotnet test --filter "TestClassName"
   ```

### GitHub Actions Failures

1. **Missing NUGET_API_KEY Secret:**
   - Go to repository Settings → Secrets and variables → Actions
   - Add `NUGET_API_KEY` secret with your NuGet API key

2. **Version Format Issues:**
   - Ensure tags follow semantic versioning: `v1.2.3` or `v1.2.3-beta`
   - Check tag creation: `git tag -l`

3. **Build Failures:**
   - Check Actions tab for detailed error logs
   - Verify .NET version compatibility
   - Ensure all dependencies are restored

### Package Consumption Issues

1. **Package Not Found:**

   ```bash
   # Clear NuGet cache
   dotnet nuget locals all --clear
   
   # Restore packages
   dotnet restore
   ```

2. **Version Conflicts:**

   ```bash
   # Check package dependencies
   dotnet list package --include-transitive
   
   # Update packages
   dotnet add package XiansAi.Lib --version 1.3.6
   ```

3. **Runtime Errors:**

   ```bash
   # Check target framework compatibility
   # Ensure .NET 9.0 is installed
   dotnet --version
   ```

### Local Development Issues

1. **Package Source Issues:**

   ```bash
   # List configured sources
   dotnet nuget list source
   
   # Remove problematic source
   dotnet nuget remove source "SourceName"
   ```

2. **Cache Problems:**

   ```bash
   # Clear package cache
   dotnet nuget locals all --clear
   
   # Clear MSBuild cache
   dotnet clean
   ```

---

## Package Dependencies

The XiansAi.Lib package includes the following dependencies:

- **Microsoft.Extensions.Hosting** (9.0.5)
- **Microsoft.Extensions.Logging.Console** (9.0.5)
- **Temporalio** (1.7.0)
- **Microsoft.SemanticKernel** (1.57.0)

### Compatibility

- **Target Framework**: .NET 9.0
- **Language Version**: C# 13.0
- **Nullable Reference Types**: Enabled
- **Implicit Usings**: Enabled

---

## Additional Resources

- [NuGet.org Package Page](https://www.nuget.org/packages/XiansAi.Lib)
- [NuGet Documentation](https://docs.microsoft.com/en-us/nuget/)
- [Semantic Versioning](https://semver.org/)
- [.NET CLI Reference](https://docs.microsoft.com/en-us/dotnet/core/tools/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
