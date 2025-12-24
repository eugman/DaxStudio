# DaxStudio Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-22

## Active Technologies

- C# (.NET Framework 4.7.2+, matching DaxStudio target) + Caliburn.Micro (MVVM), WPF, existing DaxStudio.QueryTrace infrastructure (001-visual-query-plan)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for C# (.NET Framework 4.7.2+, matching DaxStudio target)

## Code Style

C# (.NET Framework 4.7.2+, matching DaxStudio target): Follow standard conventions

## Recent Changes

- 001-visual-query-plan: Added C# (.NET Framework 4.7.2+, matching DaxStudio target) + Caliburn.Micro (MVVM), WPF, existing DaxStudio.QueryTrace infrastructure

<!-- MANUAL ADDITIONS START -->

## Documentation

Documentation for new features is in the `docs/` folder:
- `docs/VISUAL_QUERY_PLAN_PATTERNS.md` - Query plan parsing patterns and operation string formats
- `docs/VISUAL_QUERY_PLAN_SOURCES.md` - Source attribution for research consulted

## Build & Run

**Build the Standalone project (creates full executable with all dependencies):**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" src/DaxStudio.Standalone/DaxStudio.Standalone.csproj -t:Build -p:Configuration=Debug -v:minimal -m
```

**Build UI project only (faster, for UI-only changes):**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" src/DaxStudio.UI/DaxStudio.UI.csproj -t:Build -p:Configuration=Debug -v:minimal -m
```

**Restore packages first if needed:**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" src/DaxStudio.Standalone/DaxStudio.Standalone.csproj -t:Restore -p:Configuration=Debug -v:minimal
```

**Run DaxStudio:**
```powershell
Start-Process "C:\Users\eugme\Documents\GitHub\DaxStudio\src\bin\Debug\DaxStudio.exe"
```

**Executable location:** `src\bin\Debug\DaxStudio.exe`

## Build Guidelines

- **ALWAYS check build output for errors** before attempting to run the application
- Look for "error" in MSBuild output - warnings are usually OK
- If build fails, fix errors before proceeding
- Build the **Standalone** project when running the app (includes all dependencies)
- Build the **UI** project for faster iteration on UI-only changes

<!-- MANUAL ADDITIONS END -->
