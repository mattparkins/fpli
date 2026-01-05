namespace fpli {

	public static class FormCalculator {

		/// <summary>
		/// Get recent form string for a team (e.g., "WWDLWWdL")
		/// W = home win, w = away win
		/// D = home draw, d = away draw
		/// L = home loss, l = away loss
		/// Most recent result is on the RIGHT
		/// </summary>
		public static string GetFormString(int teamCode, int numGames = 8) {
			var results = GetRecentResults(teamCode, numGames);
			return string.Join("", results.Select(r => {
				// W/w for win, = for draw, - for loss
				// Lowercase w for away wins
				if (r.result == 'W') return r.isHome ? 'W' : 'w';
				if (r.result == 'D') return '=';
				if (r.result == 'L') return '-';
				return '?';
			}));
		}

		/// <summary>
		/// Get recent results for a team, ordered oldest to newest
		/// </summary>
		public static List<(char result, bool isHome, int gw, int season)> GetRecentResults(int teamCode, int numGames) {
			var allResults = new List<(char result, bool isHome, int gw, int season, DateTime kickoff)>();

			// Get from historical seasons
			History history = FPLData.Instance.History;
			foreach (var seasonKv in history.Bootstrap) {
				int season = seasonKv.Key;
				Bootstrap bootstrap = seasonKv.Value;

				if (!history.Fixtures.ContainsKey(season)) continue;

				foreach (var gwKv in history.Fixtures[season]) {
					foreach (Fixture fix in gwKv.Value) {
						if (fix.team_h_score == null || fix.team_a_score == null) continue;

						Team homeTeam = bootstrap.GetTeamFromId(fix.team_h);
						Team awayTeam = bootstrap.GetTeamFromId(fix.team_a);

						if (homeTeam == null || awayTeam == null) continue;

						int hScore = (int)fix.team_h_score;
						int aScore = (int)fix.team_a_score;

						// Check if this team was involved
						if (homeTeam.code == teamCode) {
							char result = hScore > aScore ? 'W' : (hScore == aScore ? 'D' : 'L');
							allResults.Add((result, true, gwKv.Key, season, fix.kickoff_time));
						} else if (awayTeam.code == teamCode) {
							char result = aScore > hScore ? 'W' : (aScore == hScore ? 'D' : 'L');
							allResults.Add((result, false, gwKv.Key, season, fix.kickoff_time));
						}
					}
				}
			}

			// Sort by date (most recent last) and take the last N
			return allResults
				.OrderBy(r => r.season)
				.ThenBy(r => r.gw)
				.ThenBy(r => r.kickoff)
				.TakeLast(numGames)
				.Select(r => (r.result, r.isHome, r.gw, r.season))
				.ToList();
		}

		/// <summary>
		/// Get form stats summary
		/// </summary>
		public static (int wins, int draws, int losses) GetFormStats(int teamCode, int numGames = 8) {
			var results = GetRecentResults(teamCode, numGames);
			int wins = results.Count(r => r.result == 'W');
			int draws = results.Count(r => r.result == 'D');
			int losses = results.Count(r => r.result == 'L');
			return (wins, draws, losses);
		}
	}
}
