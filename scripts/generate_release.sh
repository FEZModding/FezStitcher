#!/usr/bin/env bash

set -xeu

# Move to repository directory
cd "`dirname "$0"`/.."

rm -rf release
mkdir -p release/FezStitcher

dotnet build --configuration Release -p:ModOutputDir=./release/FezStitcher

# Generate zip
cd release/FezStitcher
zip -r ../FezStitcher.zip .
cd -
rm -rf release/FezStitcher
