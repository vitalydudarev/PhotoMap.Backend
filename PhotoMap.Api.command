#!/bin/bash

cd "$(dirname "$BASH_SOURCE")"
cd PhotoMap.Api/PhotoMap.Api/bin/Debug/net7.0

dotnet PhotoMap.Api.dll Environment=Development --urls=https://localhost:5001