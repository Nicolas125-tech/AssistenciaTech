#!/bin/bash
# Pre-commit checks
dotnet build AssistenciaTech.sln
if grep -q "options.UseSqlite" Program.cs; then
    echo "Warning: Should be using Npgsql in this step to match previous passing state"
fi
