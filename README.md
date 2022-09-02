# fpli

The idea behind the project is to allow investigation and analysis of various types parts of officialFPL's API. Presently there are two types of analysis:

- Mini League Analyser (fun commentary on mini leagues)
- Fixture Analysis (for fixture predictor games)

The engine will download the FPL bootstrap data and the current week's status, plus anything else that is required for each analyser. Some files are cached for just an hour, and others (for instance manager transfers) for much longer. These are stored in data/.cache - the .cache folder can be safely emptied as required.

## Requirements

Requires .NET to run, v5 or 6 (?)

## Usage

Examples of how to run are found in the run/ folder.

### Mini-league analysis:

The mini-league analysis puts together a fun set of analysis for each mini league - analysing which chips have been played, transfers made and captaincy before the games in a gameweek have been played, and analysis the success of the gameweek afterwards. Much more could be added to this. Usage:

dotnet run --leagueId `<league Id>` --maxManagers `<Maximum number of managers>`

dotnet run --leagueId 314 --maxManagers 100

The leagueId can be found by looking in the URL of a league table on the officialFPL website. MaxManagers defaults to 50. The engine will download two files for each manager - their team selection and transfer history.

### Fixture Analysis

The Fixture Analysis Engine is a search engine (like chess) for predictor games to maximise the chance of picking a winning team. Very much an experimental work in progress. It looks at previous form & fixtures to form an ELO rating for home/away of each team and then coverts that rating to a Win Expectancy and then tries to find the maximum Win Expectancy after playing out the selected number of gameweeks. The only constraint is that a team cannot be selected twice. The usage is:

dotnet run --executeFixtureAnalysis `<start gameweek>` `<number of gameweeks>` --previousPicks `<list of previously selected teams>`

dotnet run --executeFixtureAnalysis 1 10 --previousPicks NEW ARS TOT CHE

The fixture analyser will need to fetch the upcoming gameweeks. It cannot handle double gameweeks yet. It does not yet screen out draws from the win expectancy.
