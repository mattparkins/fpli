namespace fpli {

	public struct TeamStats {
		public int Wins;
		public int Draws;
		public int Losses;
		public int TotalMatches;

		public double WinRate => TotalMatches > 0 ? (double)Wins / TotalMatches : 0;
		public double DrawRate => TotalMatches > 0 ? (double)Draws / TotalMatches : 0;
		public double LossRate => TotalMatches > 0 ? (double)Losses / TotalMatches : 0;

		// Weakness = how often this team loses (for opponent targeting)
		public double Weakness => LossRate;
	}

	public static class EmpiricalStatsManager {

		// Keyed by team code (persistent across seasons)
		public static Dictionary<int, TeamStats> HomeStats { get; private set; } = new();
		public static Dictionary<int, TeamStats> AwayStats { get; private set; } = new();

		// League-wide baseline draw rate
		public static double LeagueDrawRate { get; private set; }

		private static bool _initialised = false;

		public static void Initialise(int numSeasons = 2) {
			HomeStats.Clear();
			AwayStats.Clear();

			History history = FPLData.Instance.History;

			// Get the most recent seasons
			var seasons = history.Bootstrap.Keys.OrderByDescending(k => k).Take(numSeasons).ToList();

			int totalMatches = 0;
			int totalDraws = 0;
			int seasonsProcessed = 0;

			foreach (int season in seasons) {
				_processSeason(season, ref totalMatches, ref totalDraws);
				seasonsProcessed++;
			}

			// Calculate league-wide draw rate
			LeagueDrawRate = totalMatches > 0 ? (double)totalDraws / totalMatches : 0.26;

			Console.WriteLine($"EmpiricalStats: Processed {seasonsProcessed} seasons, {totalMatches} matches");
			Console.WriteLine($"EmpiricalStats: League draw rate = {LeagueDrawRate:P1}");

			_initialised = true;
		}

		private static void _processSeason(int season, ref int totalMatches, ref int totalDraws) {
			History history = FPLData.Instance.History;
			Bootstrap bootstrap = history.Bootstrap[season];
			var fixtures = history.Fixtures[season];

			// Ensure all teams have entries
			foreach (Team team in bootstrap.teams) {
				if (!HomeStats.ContainsKey(team.code)) {
					HomeStats[team.code] = new TeamStats();
					AwayStats[team.code] = new TeamStats();
				}
			}

			// Process each fixture
			foreach (var gw in fixtures) {
				foreach (Fixture fix in gw.Value) {
					if (fix.team_h_score == null || fix.team_a_score == null) continue;

					Team homeTeam = bootstrap.GetTeamFromId(fix.team_h);
					Team awayTeam = bootstrap.GetTeamFromId(fix.team_a);

					if (homeTeam == null || awayTeam == null) continue;

					int hscore = (int)fix.team_h_score;
					int ascore = (int)fix.team_a_score;

					// Update home team stats
					var hStats = HomeStats[homeTeam.code];
					hStats.TotalMatches++;
					if (hscore > ascore) hStats.Wins++;
					else if (hscore == ascore) { hStats.Draws++; totalDraws++; }
					else hStats.Losses++;
					HomeStats[homeTeam.code] = hStats;

					// Update away team stats
					var aStats = AwayStats[awayTeam.code];
					aStats.TotalMatches++;
					if (ascore > hscore) aStats.Wins++;
					else if (hscore == ascore) aStats.Draws++;
					else aStats.Losses++;
					AwayStats[awayTeam.code] = aStats;

					totalMatches++;
				}
			}
		}

		// Get team's draw propensity (higher = more draws)
		public static double GetDrawPropensity(int teamCode, Venue venue) {
			var stats = venue == Venue.HOME ? HomeStats : AwayStats;
			if (stats.TryGetValue(teamCode, out var teamStats) && teamStats.TotalMatches > 0) {
				return teamStats.DrawRate;
			}
			return LeagueDrawRate;
		}

		// Get opponent weakness (how often they lose at their venue)
		public static double GetOpponentWeakness(int teamCode, Venue venue) {
			var stats = venue == Venue.HOME ? HomeStats : AwayStats;
			if (stats.TryGetValue(teamCode, out var teamStats) && teamStats.TotalMatches > 0) {
				return teamStats.Weakness;
			}
			return 0.33; // Default ~33% loss rate
		}

		// Get team's win rate at venue
		public static double GetWinRate(int teamCode, Venue venue) {
			var stats = venue == Venue.HOME ? HomeStats : AwayStats;
			if (stats.TryGetValue(teamCode, out var teamStats) && teamStats.TotalMatches > 0) {
				return teamStats.WinRate;
			}
			return venue == Venue.HOME ? 0.45 : 0.30; // PL averages
		}
	}
}
