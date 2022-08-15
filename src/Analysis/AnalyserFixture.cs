namespace fpli {
	public class FixtureAnalyser : Analyser {

		public override bool RequiresHistory { get { return true; } }
		
		public FixtureAnalyser(FPLData fpl, Config config): base(fpl, config) {
			if (_config.gameweek <= 0) {
				Program.Quit("Gameweek is invalid or not set");
			}
			
			if (_config.fixtureCount <= 0) {
				Program.Quit("FixtureCount is invalid or not set");
			}

			if (_config.fixturePicks.Count >= _config.fixtureCount) {
				Program.Quit("FixturesAlreadyPicked count >= Gameweeks to analyse");
			}
		}

		public override async Task PreFetch() {
			if (_config.gameweek + _config.fixtureCount -1 >  _fpl.Bootstrap.events.Count) {
				Program.Quit("Gameweek and Analysis window extend beyond end of the season");
			}

			int i = _config.fixtureCount;
			while (i-- > 0) {
				await _fpl.LoadFixtures(_config.gameweek + i);
			}
		}


		// Check and fix any data invalidity
		public override void Preprocess() {}

		enum Venue {
			HOME = 0,
			AWAY = 1,
		}

		int[] _minStrength = new int[2];
		int[] _maxStrength = new int[2];
		int[] _forcedMoves;

		double[,] we = new double[21,21];	// 0,0 team id's are one-indexed

		private void _calculateTeamStrengths() {
			// Use FPL home/away strengths for now, but find the max/med/min
			_minStrength[(int) Venue.HOME] = (int) _fpl.Bootstrap.teams.Min(t => t.strength_overall_home);
			_maxStrength[(int) Venue.HOME] = (int) _fpl.Bootstrap.teams.Max(t => t.strength_overall_home);
			_minStrength[(int) Venue.AWAY] = (int) _fpl.Bootstrap.teams.Min(t => t.strength_overall_away);
			_maxStrength[(int) Venue.AWAY] = (int) _fpl.Bootstrap.teams.Max(t => t.strength_overall_away);

			Console.WriteLine($"Home min/med/max strengths: {_minStrength[(int) Venue.HOME]} {_maxStrength[(int) Venue.HOME]}");
			Console.WriteLine($"Away min/med/max strengths: {_minStrength[(int) Venue.AWAY]} {_maxStrength[(int) Venue.AWAY]}");

			// Todo: foreach team, as home work out the WE against each possible opponent.

			Console.WriteLine("Win expectancy (home is left column):");
			Console.Write("     ");
			for (int a = 1; a <= 20; a++) {
				Console.Write($" {_fpl.Bootstrap.teams.Find(t => t.id == a).short_name}  ");
			}

			for (int h = 1; h <= 20; h++) {
				Console.Write($"\n{_fpl.Bootstrap.teams.Find(t => t.id == h).short_name}  ");

				for (int a = 1; a <= 20; a++) {
					we[a,h] = _winExpectancyHome(h, a);
					Console.Write($"{we[a,h]:0.00}  ");
				}
			}
			Console.WriteLine("");

			// Use double as the numbers will get small after 8 depth
			// Win expectancy is a good start, but WE includes draw points - is there a way of removing them using historic data?
			// Perhaps reducing win % by the proportion of points that come from draws?  (would have to consider win to be worth 2pts, draw 1pt)

			// Go through every fixture this week
			// _fpl.Fixtures[2].ForEach(f => {
			// 	int homeId = f.team_h;
			// 	int awayId = f.team_a;
			// 	float weHome = _winExpectancyHome(homeId, awayId);
			// 	float weAway = _winExpectancyAway(homeId, awayId);
			// 	string homeName = _fpl.Bootstrap.teams.Find(t => t.id == homeId).short_name;
			// 	string awayName = _fpl.Bootstrap.teams.Find(t => t.id == awayId).short_name;
			// 	string homeGoals = f.team_h_score?.ToString() ?? "TBD";
			// 	string awayGoals = f.team_a_score?.ToString() ?? "TBD";
			// 	Console.WriteLine($"WE for {homeName} ({weHome:0.00}) vs ({weAway:0.00}) {awayName}, actual {homeGoals}-{awayGoals} ");
			// });
		}

		private void _buildForcedMoves() {
			_forcedMoves = new int[_config.fixturePicks.Count];
			for (int i = 0; i < _config.fixturePicks.Count; i++) {
				_forcedMoves[i] = _fpl.Bootstrap.teams.Find(t => t.short_name == _config.fixturePicks[i])?.id ?? 0;
				if (_forcedMoves[i] == 0) {
					Program.Quit($"Fixture Pick {_config.fixturePicks[i]} not recognised.");
				}
			}
		}

		public override void Analyse() {
			_calculateTeamStrengths();
			_buildForcedMoves();

			int forcedMoveCount = _config.fixturePicks.Count;
			int minimumDepth = forcedMoveCount > 0 ? forcedMoveCount : 1;
			int targetDepth = minimumDepth;
			while (targetDepth++ < _config.fixtureCount) {
				
				// Initialise engine

				TeamScoreEval[] eval = new TeamScoreEval[targetDepth];

				int depth = targetDepth;

				// Play forced moves
				while (depth > targetDepth - forcedMoveCount) {
					depth--;
					eval[depth].team = _forcedMoves[targetDepth - depth];
					eval[depth].eval = 1f;
				} 
				
				// 

			}

			// For each searchdepth from forcedMoveCount+1 to targetDepth
			// 		Initialise engine
			// 		Play forced moves
			// 		Enumerate "root" moves
			// 		Foreach root move recurse
			//			Make move
			// 				If not at leaf 
			//					enumerate moves
			//					foreach move recurse
			//				else 
			//					evaluate
			//			undo move
			//		return best evaluation
		}

		float _winExpectancyHome(int homeId, int awayId) {
			Team home = _fpl.Bootstrap.teams.Find(t => t.id == homeId);
			Team away = _fpl.Bootstrap.teams.Find(t => t.id == awayId);

			float dr = (away.strength_overall_away - home.strength_overall_home);
			return (float) (1f/(Math.Pow(10f, dr/400f) +1f));
		}

		float _winExpectancyAway(int homeId, int awayId) {
			Team home = _fpl.Bootstrap.teams.Find(t => t.id == homeId);
			Team away = _fpl.Bootstrap.teams.Find(t => t.id == awayId);

			float dr = (home.strength_overall_home - away.strength_overall_away);
			return (float) (1f/(Math.Pow(10f, dr/400f) +1f));
		}
	}

	public struct TeamScoreEval {
		public int team;
		public double eval;
	}
}