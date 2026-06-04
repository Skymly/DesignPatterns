#!/usr/bin/env pwsh
$BuildProject = Join-Path $PSScriptRoot "build" "_build.csproj"
dotnet run --project $BuildProject -- @args
