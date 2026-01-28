# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the project
dotnet build

# Build for Release
dotnet build --configuration Release

# Run the application
dotnet run
```

## Project Overview

This is a WPF desktop application targeting .NET 10.0 for Windows. The project uses:
- SDK-style project file (`mPrismaMapsWPF.csproj`)
- Nullable reference types enabled
- Implicit usings enabled (System, System.Collections.Generic, System.Linq, System.Threading, System.Threading.Tasks)
- Traditional WPF code-behind pattern with XAML UI definitions

## Architecture

- **App.xaml / App.xaml.cs**: Application entry point and startup configuration
- **MainWindow.xaml / MainWindow.xaml.cs**: Primary UI window
- **AssemblyInfo.cs**: Assembly metadata

The solution file is `mPrismaMapsWPF.slnx` (modern VS solution format).
