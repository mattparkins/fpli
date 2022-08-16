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
		double[,] teamFixtureWE;			// gameweek, teamid

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

			// Populate teamFixtureWE
			teamFixtureWE = new double[_config.fixtureCount +1, 21];

			for (int gw = _config.gameweek; gw < _config.gameweek + _config.fixtureCount; gw++) {
				int index = _config.gameweek + _config.fixtureCount - gw;

				// For every fixture in this gameweek
				_fpl.Fixtures[gw].ForEach(fix => {
					teamFixtureWE[index, fix.team_h] = _winExpectancyHome(fix.team_h, fix.team_a);
					teamFixtureWE[index, fix.team_a] = _winExpectancyAway(fix.team_h, fix.team_a);
				});
			}


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

			const int resetBoard = (1 << 21) -2; // 0x1FFFFE, 20 bits set, offset by 1

			int forcedMoveCount = _config.fixturePicks.Count;
			int minimumDepth = forcedMoveCount +1;
			int targetDepth = minimumDepth -1;
			while (++targetDepth < _config.fixtureCount) {
				
				// Initialise engine

				// The board is a bit array where the team id is an index to the bit
				// which if set has been used at this level or higher
				int[] board = new int[targetDepth +1];  
				for (int i = 0; i <= targetDepth; i++) {
					board[i] = resetBoard;
				}
				
				// The index into each move, which coincidentally is also the teamid
				int[] moveIndex = new int[targetDepth];

				TeamScoreEval[] eval = new TeamScoreEval[targetDepth];
				int depth = targetDepth;

				// Play forced moves
				while (depth > targetDepth - forcedMoveCount) {
					int teamId = _forcedMoves[targetDepth - depth];
					depth--;
					eval[depth].team = teamId;
					eval[depth].eval = 1f;
					board[depth] = board[depth +1] & ~(1 << teamId);
				} 
				
				// Iterate through root moves
				double bestScore = 0;
				TeamScoreEval[] bestLine = null;

				depth--;
				int rootDepth = depth;
				while (moveIndex[rootDepth] <= 20) {
					
					while (moveIndex[depth] <= 20) {

						// Go the next move
						moveIndex[depth]++;
						board[depth] >>= 1;
						
						// Has this team already been used ?
						if ((board[depth] & 1) == 1) {
							
							// No, so add to the eval and make the move 
							eval[depth].team = moveIndex[depth];	// Add team to the eval
							eval[depth].eval = eval[depth +1].eval * teamFixtureWE[targetDepth -depth, moveIndex[depth]];	// Multiply the score in
							board[depth] = board[depth +1] & ~(1 << moveIndex[depth]);							// Mark team as used

							// If we're not at the leaf we need to set up the recurse
							if (depth > 0) {
								depth--;
								moveIndex[depth] =0;
								board[depth] = board[depth +1];
							} else {
								// If we're at the leaf, check if it's a new best score
								if (eval[depth].eval > bestScore) {
									bestScore = eval[depth].eval;
									bestLine = (TeamScoreEval[]) eval.Clone();
									_displayLine(bestLine);
								}
							}
						}
					}

					//We've finished this depth's moves, move higher
					depth++;
				}
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

		double _winExpectancyHome(int homeId, int awayId) {
			Team home = _fpl.Bootstrap.teams.Find(t => t.id == homeId);
			Team away = _fpl.Bootstrap.teams.Find(t => t.id == awayId);

			double dr = (away.strength_overall_away - home.strength_overall_home);
			return 1f/(Math.Pow(10f, dr/400f) +1f);
		}

		double _winExpectancyAway(int homeId, int awayId) {
			Team home = _fpl.Bootstrap.teams.Find(t => t.id == homeId);
			Team away = _fpl.Bootstrap.teams.Find(t => t.id == awayId);

			double dr = (home.strength_overall_home - away.strength_overall_away);
			return 1f/(Math.Pow(10f, dr/400f) +1f);
		}


		// A line starts at the node and works to the root though we'll need to 
		// display it in inverse order

		void _displayLine(TeamScoreEval[] line) {
			Console.Write($"Depth {line.Length}, best {line[0].eval:0.000}:");
			for (int i = line.Length -1; i >= 0; --i) {
				string teamName = _fpl.Bootstrap.teams.Find(t => t.id == line[i].team).short_name;
				Console.Write($"  {teamName} {line[i].eval:0.000}");
			}
			Console.WriteLine("");
		}
	}

	public struct TeamScoreEval {
		public int team;
		public double eval;
	}
}