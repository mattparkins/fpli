using System.Text;
using Spectre.Console;

namespace fpli {

	public enum Venue {
		HOME = 0,
		AWAY = 1,
	}

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
		public override void Preprocess() {
			EloManager.Initialise(40);
		}

		DateTime analysisStart;

		int[] _minStrength = new int[2];
		int[] _maxStrength = new int[2];
		int[] _forcedMoves;
		int[] _excludedTeams;

		double[,] we = new double[21,21];	// 0,0 team id's are one-indexed
		double[,] teamFixtureWE;			// gameweek, teamid

		private void _calculateTeamStrengths() {
		
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
				//int index = _config.gameweek + _config.fixtureCount - gw;
				int index = gw -_config.gameweek +1;

				Console.WriteLine($"\nFixtures for GW{gw}");

				// For every fixture in this gameweek
				_fpl.Fixtures[gw].ForEach(fix => {
					teamFixtureWE[index, fix.team_h] = _winExpectancyHome(fix.team_h, fix.team_a);
					teamFixtureWE[index, fix.team_a] = _winExpectancyAway(fix.team_h, fix.team_a);

					string hname = _fpl.Bootstrap.teams.Find(t => t.id == fix.team_h).short_name;
					string aname = _fpl.Bootstrap.teams.Find(t => t.id == fix.team_a).short_name;

					Console.WriteLine($"{hname} {teamFixtureWE[index, fix.team_h]:0.000} vs {teamFixtureWE[index, fix.team_a]:0.000} {aname} ");
				});
			}

			// Win expectancy is a good start, but WE includes draw points - is there a way of removing them using historic data?
			// Perhaps reducing win % by the proportion of points that come from draws?  (would have to consider win to be worth 2pts, draw 1pt)
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

		// Build the exclude teams array

		private void _buildExcludedTeams() {
			_excludedTeams = new int[_config.excludeTeams.Count];
			for (int i = 0; i < _config.excludeTeams.Count; i++) {
				_excludedTeams[i] = _fpl.Bootstrap.teams.Find(t => t.short_name == _config.excludeTeams[i])?.id ?? 0;
				if (_excludedTeams[i] == 0) {
					Program.Quit($"Exclude Team {_config.excludeTeams[i]} not recognised.");
				}
			}
		}


		double _winExpectancyHome(int homeId, int awayId) {
			Team home = _fpl.Bootstrap.teams.Find(t => t.id == homeId);
			Team away = _fpl.Bootstrap.teams.Find(t => t.id == awayId);

			Elo awayElo = EloManager.TeamElo[Venue.AWAY][away.code];
			Elo homeElo = EloManager.TeamElo[Venue.HOME][home.code];

			double awayRating = awayElo.Rating;
			double homeRating = homeElo.Rating;
			double dr = awayRating - homeRating;
			double we = 1.0/(Math.Pow(10.0, dr/400.0) +1.0);
			
			return we;
		}


		double _winExpectancyAway(int homeId, int awayId) {
			Team home = _fpl.Bootstrap.teams.Find(t => t.id == homeId);
			Team away = _fpl.Bootstrap.teams.Find(t => t.id == awayId);

			Elo awayElo = EloManager.TeamElo[Venue.AWAY][away.code];
			Elo homeElo = EloManager.TeamElo[Venue.HOME][home.code];

			double awayRating = awayElo.Rating;
			double homeRating = homeElo.Rating;
			double dr = homeRating - awayRating;
			double we = 1.0/(Math.Pow(10.0, dr/400.0) +1.0);

			return we;
		}


		// A line starts at the node and works to the root though we'll need to 
		// display it in inverse order.  The alg above requires one extra depth sometimes
		// so we need to pass in the depth rather than infer it from the line.length

		void _displayLine(TeamScoreEval[] line, int depth, Int64 nodes) {
			
			if (line == null) {
				return;
			}

			double mn = nodes / 1000000d;
			TimeSpan diff = DateTime.UtcNow.Subtract(analysisStart);
			double nps = mn / diff.TotalSeconds;

			_sb.Clear();
			_sb.Append($"{diff.TotalSeconds,4:0.0}s Depth {depth}, best {line[depth-1].eval:0.000}:");
			for (int i = 0; i < depth; i++) {
				string teamName = _fpl.Bootstrap.teams.Find(t => t.id == line[i].team).short_name;
				
				// is this team away?  If so lowercase it.
				if (_fpl.Fixtures[_config.gameweek +i].FindIndex(fix => fix.team_a == line[i].team) != -1) {
					teamName = teamName.ToLower();
				}
				
				_sb.Append($"  {teamName} {line[i].eval:0.000}");
			}
			
			_sb.Append($"  ({mn:0.00}m nodes at {nps:0.0}m/s)");
			AnsiConsole.MarkupLine(_sb.ToString());
		}


		// Analyse the fixtures, v2 rewrite, with parallelisation

		public override void Analyse() {

			// Add a locking mechanism
			Object lockO = new Object();

			_calculateTeamStrengths();
			_buildForcedMoves();
			_buildExcludedTeams();

			analysisStart = DateTime.UtcNow;
			Console.WriteLine($"\nEngine started {analysisStart.ToShortTimeString()}");
	
			Console.WriteLine();

			// The defaultBoard is a set of flags, one for each team, that are set to 1 if the team is available
			// or zero if the team is to be ignored (either because it's been played or excluded).

			int defaultBoard = (1 << 21) -2; // 0x1FFFFE, 20 bits set, offset by 1
			for (int i = 0; i < _excludedTeams.Length; i++) {
				defaultBoard &= ~(1 << _excludedTeams[i]);
			}

			Int64 nodes = 0;					// Total number of nodes visited
			double bestScore = 0;
			TeamScoreEval[] bestLine = null;	// Best line found so far

			// Forced moves are the moves that have already been played by the user in the game
			// as taken from the config.  We will play these moves first and then our analysis
			// begins after those moves.

			int forcedMoveCount = _config.fixturePicks.Count; 	// The number of moves already played by the user
			int minimumDepth = forcedMoveCount +1;				// The starting depth for our analysis
			int targetDepth = minimumDepth -1;					// The target depth, incremented each time we complete a depth

			// While loop iterates from the minimum depth to the maximum depth
			// Essentially, we repeat the analysis for each depth.

			while (++targetDepth <= _config.fixtureCount) {

				bestScore = 0;

				// availableTeams is an array where each element is a bitflag representing available teams
				// at a specific depth.  If a bit is set to 1 in board[i], that team is available for
				// selection at depth i.  If a bit is cleared to 0, that team has already been used at either
				// this depth or a shallower depth.

				int[] availableTeams = new int[targetDepth +1]; 
				availableTeams[0] = defaultBoard;

				// moveStack tracks the current team being considered for selection at each depth.
				// Each element represents the team ID currently under consideration at that depth.

				int[] moveStack = new int[targetDepth];

				// Eval represents the score each depth, all subsequent depths are based on this score
				// It starts at 1.0 and is multiplied by the win expectancy of the team being considered at each 
				// depth, thus reducing the score (the chance of winning every game) as we go further in time.

				TeamScoreEval[] eval = new TeamScoreEval[targetDepth +1];

				// Apply forced moves - the moves the user has already successfully played

				int currentDepth = -1;
				while (++currentDepth < forcedMoveCount) {

					int teamId = _forcedMoves[currentDepth];
					moveStack[currentDepth] = teamId;
					eval[currentDepth].team = teamId;
					eval[currentDepth].eval = 1f;
					
					// Set up the available teams for the next depth
					availableTeams[currentDepth +1] = availableTeams[currentDepth] & ~(1 << teamId);
				}

				// Create the progress bar 

				AnsiConsole.Progress()
					.AutoClear(true)   		// Do not remove the task list when done
					.HideCompleted(true)   	// Hide tasks as they are completed
					.Start(ctx => {
					
					// How many nodes are we searching?  Think we need at least 100m nodes to make it 
					// worth showing the progress bars

					Int64 anticipatedNodes = 5;
					for (int i = currentDepth; i < targetDepth; i++) {
						anticipatedNodes *= 20 - currentDepth - _excludedTeams.Count();
					}

					// Create a task for each parallel element
					var progressBars = new Dictionary<int, ProgressTask>();
					for (int i = 1; i <= 20; i++) {
						if (anticipatedNodes > 200_000_000 && (availableTeams[currentDepth] & (1 << i)) != 0) {
							string teamName = _fpl.Bootstrap.teams.Find(t => t.id == i).short_name;
							progressBars.Add(i, ctx.AddTask($"{i} {teamName}", autoStart: true));
						} else {
							progressBars.Add(i, null);
						}
					}

					// Parallel For loop iterates through the root moves.
					// For ease of development and bug prevention, we'll place as much as we can into a function

					// ParallelOptions parallelOptions = new ParallelOptions {
					// 	MaxDegreeOfParallelism = 24 // or 24, depending on your preference
					// };

					//Parallel.For(1, 21, parallelOptions, (rootMoveIndex) => {
					Parallel.For(1, 21, (rootMoveIndex) => {

						// If  this team already been played or excluded then skip it

						if ((availableTeams[currentDepth] & (1 << rootMoveIndex)) == 0) {
							return;
						}

						// Set off the analysis for this branch

						(TeamScoreEval[] branchEval, Int64 branchNodes) = _analyseFixtures(availableTeams[currentDepth], rootMoveIndex, currentDepth, targetDepth, progressBars[rootMoveIndex]);	

						// Lock for sync

						lock (lockO) {

							TeamScoreEval[] fullBranchEval = eval.Clone() as TeamScoreEval[];
							Array.Copy(branchEval, 0, fullBranchEval, currentDepth, branchEval.Length);

							nodes += branchNodes;
							
							if (fullBranchEval[targetDepth -1].eval > bestScore) {
								bestScore = fullBranchEval[targetDepth -1].eval;
								bestLine = fullBranchEval.Clone() as TeamScoreEval[];
							}
						}
					});

					// Finish the progress bars
					foreach (var (tid,bar) in progressBars) {
						bar?.StopTask();
					}

					_displayLine(bestLine, targetDepth, nodes);
				});
			}
			
			AnsiConsole.MarkupLine("---Search Complete");
		}


		// Analyse the fixtures for a specific team from and to specific depths
		public (TeamScoreEval[] branchEval, Int64 branchNodes) _analyseFixtures(int teamsFlag, int rootMoveIndex, int entryDepth, int targetDepth, ProgressTask bar) {

			double bestScore = 0;
			TeamScoreEval[] bestLine;

			int localTargetDepth = targetDepth - entryDepth;
			Int64 branchNodes = 0;
			TeamScoreEval[] branchEval = new TeamScoreEval[localTargetDepth +1];
			int[] availableTeams = new int[localTargetDepth +1];

			// Play this branch's root move
			int localDepth = 0;
			branchEval[localDepth].team = rootMoveIndex;
			branchEval[localDepth].eval = teamFixtureWE[entryDepth +1, rootMoveIndex]; // 1-indexed, the first gameweek(GW1) is index 1
			availableTeams[localDepth] = teamsFlag;
			localDepth++;
			branchNodes++;

			//Set up the best score + line so far
			bestLine = branchEval.Clone() as TeamScoreEval[];
			
			// If we're not at the leaf we need to advance deeper, for now just replicate the our branch's root node
			if (localTargetDepth > 1) {
				
				// Traverse every combination of team and depth.
				// Advance the node we're currently in until it hits a valid team or 21
				//  - If this node is a valid team then evaluate this node.
				//		- If this node is a leaf then evaluate against the best line
				//		- If this node is not a leaf then reset the deeper node and move into it
				//  - If this node is not a valid team (21) then simply move shallower

				branchEval[1].team = 0;
				availableTeams[1] = availableTeams[0] & ~(1 << branchEval[0].team);

				// Calculate how many progress updates we'll have - if we do just the one update
				// per root move we won't put too much pressure on the UI thread.  The number of
				// updates is simply the hamming weight

				double barInc = 1.0 / (Utils.HammingWeight(availableTeams[1]) +1) * 100.0;
				int lastUpdateTeam = 0;
				

				while (branchEval[1].team <= 20) {
					branchNodes++;

					// Advance the node we're currently in until it hits a valid team or 21
					branchEval[localDepth].team++;
					while (branchEval[localDepth].team <= 20 && (availableTeams[localDepth] & (1 << branchEval[localDepth].team)) == 0) {
						branchEval[localDepth].team++;
					}

					// If this node is a valid team then evaluate this node.
					if (branchEval[localDepth].team <= 20) {
						branchEval[localDepth].eval = branchEval[localDepth -1].eval * teamFixtureWE[entryDepth + localDepth +1, branchEval[localDepth].team];

						// If this node is a leaf then evaluate against the best line
						if (localDepth +1 == localTargetDepth) {

							if (branchEval[localDepth].eval > bestScore) {
								bestScore = branchEval[localDepth].eval;
								bestLine = branchEval.Clone() as TeamScoreEval[];
							}

						} else {

							// Update the progress bar if need be
							
							if (localDepth <= 1 && lastUpdateTeam != branchEval[1].team) {
								bar?.Increment(barInc);
								lastUpdateTeam = branchEval[1].team;
							}

							// This node is not a leaf - prepare to go deeper.
							// Remove the played team from the next depth's available teams
							// Reset the next depth's team index
							
							availableTeams[localDepth +1] = availableTeams[localDepth] & ~(1 << branchEval[localDepth].team);
							localDepth++;
							branchEval[localDepth].team = 0;
							
						}

					} else {
						//  This node is not a valid team (21) so simply move shallower
						localDepth--;
					}
				}

				bar?.Increment(barInc);
			}
				
			// return eval and nodes
			return (bestLine, branchNodes);
		}
	}

	public struct TeamScoreEval {
		public int team;
		public double eval;
	}

	
}