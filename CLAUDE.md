# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FPLI is a C# console application for analyzing Fantasy Premier League (FPL) data. It provides three analysis types:
- **Mini League Analysis** - Commentary and statistics for FPL mini leagues
- **Fixture Analysis** - Elo-based prediction engine for fixture predictor games
- **Export for AI** - CSV export for machine learning

## Build and Run Commands

```bash
# Build
dotnet build fpli.csproj

# Run mini-league analysis
dotnet run --leagueId <league_id> --maxManagers <count>
dotnet run --leagueId 314 --maxManagers 100

# Run fixture analysis
dotnet run --executeFixtureAnalysis <start_gw> <num_gws> --previousPicks <teams>
dotnet run --executeFixtureAnalysis 1 10 --previousPicks NEW ARS TOT CHE

# Export data for AI
dotnet run --exportForAI <gameweek>

# Development watch mode
dotnet watch run --project fpli.csproj
```

The `--callrate` parameter controls API request rate (default: 1 call/second).

## Architecture

```
CLI Args → Config.cs → Engine.cs → Analyser
                           ↓
                      FPLData.cs (singleton data manager)
                           ↓
                      Fetcher.cs (HTTP + file-based caching)
```

**Execution flow:** Each analyser follows `PreFetch() → Preprocess() → Analyse()`

**Key components:**
- `src/Config.cs` - CLI argument parsing, defines Intent enum
- `src/Engine.cs` - Orchestrator, creates analysers via factory pattern
- `src/Fetcher.cs` - HTTP client with time-based caching to `data/.cache/`
- `src/Model/FPLData.cs` - Central data repository (singleton)
- `src/Analysis/AnalyserBase.cs` - Abstract base class for all analysers
- `src/EloManager.cs` - Elo rating system for fixture predictions

**Data models** in `src/Model/FPL/` map directly to FPL API JSON responses.

## Data and Caching

- `data/.cache/` - Runtime cache (safe to delete)
- `data/historic/` - Historical season data (2021-2025) used for Elo calculations
- Cache TTLs vary: bootstrap (1 hour), manager data (300 days), picks (6 hours)

## FPL API

Base URL: `https://fantasy.premierleague.com/api/`

Key endpoints:
- `/bootstrap-static/` - Teams, players, game settings
- `/leagues-classic/{id}/standings/` - League standings (paginated)
- `/entry/{id}/event/{gw}/picks/` - Manager's team selection
- `/entry/{id}/transfers/` - Manager's transfer history
- `/fixtures/?event={gw}` - Gameweek fixtures

## Code Style

Per `.editorconfig`: opening braces on same line, else/catch/finally on same line as closing brace.
