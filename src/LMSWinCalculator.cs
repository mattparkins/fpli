namespace fpli {

	public struct LMSPrediction {
		public double ProbWin;          // P(WIN) for LMS - this is what matters
		public double ProbDraw;         // P(DRAW) - elimination risk
		public double ProbLoss;         // P(LOSS) - also elimination
		public double DrawRisk;         // Relative draw risk (1.0 = league average)
		public double EloWinExpectancy; // Raw Elo output for comparison

		public override string ToString() {
			return $"Win:{ProbWin:P0} Draw:{ProbDraw:P0} Loss:{ProbLoss:P0} DrawRisk:{DrawRisk:0.00}x";
		}
	}

	public static class LMSWinCalculator {

		/// <summary>
		/// Calculate LMS win probability for a team in a specific fixture
		/// </summary>
		/// <param name="teamCode">The team's persistent code</param>
		/// <param name="opponentCode">The opponent's persistent code</param>
		/// <param name="venue">HOME or AWAY for the team we're calculating for</param>
		public static LMSPrediction Calculate(int teamCode, int opponentCode, Venue venue) {

			// 1. Get Elo-based win expectancy
			double eloWE = _getEloWinExpectancy(teamCode, opponentCode, venue);

			// 2. Get draw probability using EMA-tracked propensities + Gaussian Elo adjustment
			double drawRate = DrawPropensityManager.CalculateDrawProbability(teamCode, opponentCode, venue);

			// 3. Convert to three-outcome probabilities
			// The remaining probability (non-draw) is split by Elo expectancy
			double remainingProb = 1.0 - drawRate;

			// Elo expectancy tells us win share of decisive outcomes
			double probWin = remainingProb * eloWE;
			double probLoss = remainingProb * (1.0 - eloWE);
			double probDraw = drawRate;

			// Calculate draw risk relative to league average
			double drawRisk = drawRate / DrawPropensityManager.LeagueDrawRate;

			return new LMSPrediction {
				ProbWin = probWin,
				ProbDraw = probDraw,
				ProbLoss = probLoss,
				DrawRisk = drawRisk,
				EloWinExpectancy = eloWE
			};
		}

		private static double _getEloWinExpectancy(int teamCode, int opponentCode, Venue venue) {
			if (!EloManager.TeamElo[venue].ContainsKey(teamCode)) {
				return venue == Venue.HOME ? 0.55 : 0.40; // Fallback
			}

			Venue oppVenue = venue == Venue.HOME ? Venue.AWAY : Venue.HOME;

			if (!EloManager.TeamElo[oppVenue].ContainsKey(opponentCode)) {
				return venue == Venue.HOME ? 0.55 : 0.40; // Fallback
			}

			Elo teamElo = EloManager.TeamElo[venue][teamCode];
			Elo oppElo = EloManager.TeamElo[oppVenue][opponentCode];

			double dr = oppElo.Rating - teamElo.Rating;
			return 1.0 / (Math.Pow(10.0, dr / 400.0) + 1.0);
		}

	}
}
