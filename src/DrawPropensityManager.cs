namespace fpli {

	public class DrawPropensity {
		public double Rate { get; private set; }
		public int MatchCount { get; private set; }

		// Alpha controls how fast old results decay
		// 0.07 means ~50% decay after 14 games (half a season memory)
		private const double Alpha = 0.07;
		private const double LeagueAverage = 0.26;

		public DrawPropensity() {
			Rate = LeagueAverage;  // Start at league average
			MatchCount = 0;
		}

		public void Update(bool wasDraw) {
			// EMA update: new_rate = α × observation + (1-α) × old_rate
			double observation = wasDraw ? 1.0 : 0.0;
			Rate = Alpha * observation + (1 - Alpha) * Rate;
			MatchCount++;
		}

		// Confidence weight based on sample size
		// Returns 0-1, reaches ~0.9 after ~20 games
		public double Confidence => 1.0 - Math.Exp(-MatchCount / 10.0);
	}

	public static class DrawPropensityManager {

		// Keyed by team code (persistent across seasons)
		public static Dictionary<int, DrawPropensity> HomePropensity { get; private set; } = new();
		public static Dictionary<int, DrawPropensity> AwayPropensity { get; private set; } = new();

		// League-wide baseline (also EMA-tracked)
		public static double LeagueDrawRate { get; private set; } = 0.26;
		private static int _leagueMatchCount = 0;
		private const double LeagueAlpha = 0.02;  // Slower decay for league average

		public static void Initialise() {
			HomePropensity.Clear();
			AwayPropensity.Clear();
			LeagueDrawRate = 0.26;
			_leagueMatchCount = 0;

			History history = FPLData.Instance.History;

			// Process all historical seasons in chronological order
			var seasons = history.Bootstrap.Keys.OrderBy(k => k).ToList();

			foreach (int season in seasons) {
				Console.WriteLine($"DrawPropensity processing {season} season");
				_processSeason(season);
			}

			Console.WriteLine($"DrawPropensity: League draw rate = {LeagueDrawRate:P1} ({_leagueMatchCount} matches)");
		}

		private static void _processSeason(int season) {
			History history = FPLData.Instance.History;
			Bootstrap bootstrap = history.Bootstrap[season];
			var fixtures = history.Fixtures[season];

			// Ensure all teams have entries
			foreach (Team team in bootstrap.teams) {
				if (!HomePropensity.ContainsKey(team.code)) {
					HomePropensity[team.code] = new DrawPropensity();
					AwayPropensity[team.code] = new DrawPropensity();
				}
			}

			// Process fixtures in gameweek order
			foreach (var gw in fixtures.OrderBy(f => f.Key)) {
				foreach (Fixture fix in gw.Value) {
					if (fix.team_h_score == null || fix.team_a_score == null) continue;

					Team homeTeam = bootstrap.GetTeamFromId(fix.team_h);
					Team awayTeam = bootstrap.GetTeamFromId(fix.team_a);

					if (homeTeam == null || awayTeam == null) continue;

					int hscore = (int)fix.team_h_score;
					int ascore = (int)fix.team_a_score;
					bool wasDraw = hscore == ascore;

					// Update team propensities
					HomePropensity[homeTeam.code].Update(wasDraw);
					AwayPropensity[awayTeam.code].Update(wasDraw);

					// Update league average
					LeagueDrawRate = LeagueAlpha * (wasDraw ? 1.0 : 0.0) + (1 - LeagueAlpha) * LeagueDrawRate;
					_leagueMatchCount++;
				}
			}
		}

		/// <summary>
		/// Get team's current draw propensity at venue, blended with league average based on confidence
		/// </summary>
		public static double GetDrawPropensity(int teamCode, Venue venue) {
			var propensities = venue == Venue.HOME ? HomePropensity : AwayPropensity;

			if (propensities.TryGetValue(teamCode, out var prop)) {
				// Blend team rate with league average based on confidence
				return prop.Rate * prop.Confidence + LeagueDrawRate * (1 - prop.Confidence);
			}
			return LeagueDrawRate;
		}

		/// <summary>
		/// Calculate draw probability for a specific matchup using:
		/// 1. Team draw propensities (EMA-tracked)
		/// 2. Gaussian adjustment based on Elo gap (close games draw more)
		/// </summary>
		public static double CalculateDrawProbability(int teamCode, int opponentCode, Venue venue) {
			Venue oppVenue = venue == Venue.HOME ? Venue.AWAY : Venue.HOME;

			// Get team draw propensities
			double teamProp = GetDrawPropensity(teamCode, venue);
			double oppProp = GetDrawPropensity(opponentCode, oppVenue);

			// Base draw rate from team propensities (average of both teams)
			double baseDrawRate = (teamProp + oppProp) / 2.0;

			// Get Elo gap for Gaussian adjustment
			double eloGap = _getEloGap(teamCode, opponentCode, venue);

			// Gaussian adjustment: draws peak when teams are evenly matched
			// σ = 250 means draw rate halves at ~295 Elo difference
			double sigma = 250.0;
			double gapFactor = Math.Exp(-(eloGap * eloGap) / (2 * sigma * sigma));

			// The gap factor reduces draw probability for mismatches
			// But we don't want to reduce it below some floor (even big mismatches sometimes draw)
			double minGapFactor = 0.4;
			gapFactor = minGapFactor + (1.0 - minGapFactor) * gapFactor;

			double adjustedDrawRate = baseDrawRate * gapFactor;

			// Cap at reasonable bounds
			return Math.Clamp(adjustedDrawRate, 0.05, 0.45);
		}

		private static double _getEloGap(int teamCode, int opponentCode, Venue venue) {
			Venue oppVenue = venue == Venue.HOME ? Venue.AWAY : Venue.HOME;

			if (!EloManager.TeamElo[venue].ContainsKey(teamCode) ||
				!EloManager.TeamElo[oppVenue].ContainsKey(opponentCode)) {
				return 0;  // No data, assume even match
			}

			double teamRating = EloManager.TeamElo[venue][teamCode].Rating;
			double oppRating = EloManager.TeamElo[oppVenue][opponentCode].Rating;

			return Math.Abs(teamRating - oppRating);
		}
	}
}
