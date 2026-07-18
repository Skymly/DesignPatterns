#!/usr/bin/env pwsh
$BuildProject = Join-Path -Path $PSScriptRoot -ChildPath "build/_build.csproj"
dotnet run --project $BuildProject -- @args
