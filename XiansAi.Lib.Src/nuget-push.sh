#!/bin/bash

# Check if version and api key are provided
if [ $# -ne 2 ]; then
    echo "Usage: $0 <version> <api_key>"
    echo "Example: $0 1.0.0 your-api-key-here"
    exit 1
fi

VERSION=$1
API_KEY=$2

# Clean and build in Release configuration
echo "Cleaning solution..."
dotnet clean

echo "Building solution in Release configuration..."
dotnet build -c Release

# Create nupkg directory and pack with specified version
echo "Creating packages..."
mkdir -p nupkg
dotnet pack -c Release -o ./nupkg /p:Version=$VERSION

# Push the package to NuGet
echo "Pushing packages to NuGet..."
dotnet nuget push ./nupkg/XiansAi.Lib.$VERSION.nupkg -s https://api.nuget.org/v3/index.json -k $API_KEY

echo "Finished pushing packages version $VERSION to NuGet"
