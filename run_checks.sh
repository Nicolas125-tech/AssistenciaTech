#!/bin/bash
# Pre-commit checks
dotnet build
dotnet test
if grep -q "options.UseNpgsql" Program.cs; then
    echo "Warning: Should be using Sqlite in this step to match previous passing state"
fi
