using System.Runtime.InteropServices;

namespace fpli {
	public class MiniLeagueAnalyser : Analyser {

		LeagueStandings _standings;
		

		public MiniLeagueAnalyser(FPLData fpl, Config config) : base(fpl, config)
		{
			// Basic config sanity check
			if (_config.leagueId <= 0)
			{
				Program.Quit("LeagueId is invalid or not set");
			}

			if (_config.maxManagers <= 0)
			{
				Program.Quit("MaxManagers is invalid");
			}
		}

		public override async Task PreFetch()
		{

			// Load the league
			await _fpl.LoadLeague(_config.leagueId, _config.maxManagers);

			// Load each manager in the league
			foreach (var standingEntry in _fpl.Standings[_config.leagueId].standings.results)
			{
				await _fpl.LoadManager(standingEntry.entry);
			}


			//Load each manager in the new entry section
			// if (_fpl.Standings[_config.leagueId].new_entries != null)
			// {
			// 	foreach (var entry in _fpl.Standings[_config.leagueId].new_entries.results)
			// 	{
			// 		int entryId = int.Parse(entry);
			// 		await _fpl.LoadManager(entryId);
			// 	}
			// }
		}


		// Check and fix any data invalidity
		public override void Preprocess() {

			// Pre-GW1, not sure what to do, not mini-league analysis thats's for sure.
			if (_fpl.Bootstrap.GetCurrentGameweekId() <= 0) {
				Program.Quit("Cannot execute Mini-League Analysis if GW is <= 0");
			}
		}


		public override void Analyse() {
			_standings = _fpl.Standings[_config.leagueId];

			_reportChipUsage();
			_reportCaptaincy();
			_reportHits();
			_reportBurnedTransfers();

			_writeSectionBreak(3);
			_writeHeader("PostGW Analysis:");
			_writeBlankLine();

			_reportPoints();
			_reportBiggestRankMoves();
			_reportPointsOnBench();
			_reportTransferSummary();
			_reportTransferDetail();
			_reportCaptaincyReturns();

			_writeSectionBreak();
			_writeHeader("Season stats:");

			_reportMostPointsOnBenchTable();
			_reportHighestScoresTable();
			_reportCaptaincyTable();
			_reportTCLeaderboard();
			//_reportAMLeaderboard();

			_reportHighestValueTeamTable();
			_reportMostHitsTable();
			_reportNoHitClub();
		}


		void _reportChipUsage() {

			int totalChipsPlayed = 0;
			var list = _fpl.Standings[_config.leagueId].ChipUsage.OrderByDescending(v => v.Value.Count);

			foreach(var kv in list) {
				if (kv.Key != "none") {
					totalChipsPlayed += kv.Value.Count;
				}
			}

			if (totalChipsPlayed == 0) {
				_writeBlankLine();
				_writeHeader("No Active Chips");
				_writeBlankLine();
				return;
			}

			_writeBlankLine();
			_writeHeader("Active Chips");
			_writeBlankLine();

			foreach(var kv in list) {
				if (kv.Key != "none") {

					// Wildcard, Bench Boost, Free Hit - chips that don't target a specific player

					if (kv.Key == "wildcard" || kv.Key == "bboost" || kv.Key == "freehit") {
						Console.Write($"{kv.Value.Count, 4} {kv.Key}: (");
						string glue = "";
						kv.Value.ForEach(entryId => {

							// Get the name of the manager, we could load this from a manager info file
							// but it's one less fetch per entry to just take it from the leaderboard

							Manager manager = _fpl.Managers[entryId];
							Result result = _fpl.Standings[_config.leagueId].standings.results.Find(r => r.entry == entryId);
							Console.Write($"{glue}{Utils.StandardiseName(result.player_name)}");
							glue = ", ";
						});
						Console.WriteLine(")");
					} else {
						// Triple captain or assistant manager - chips that DO target a specific elementId
						string chipName = kv.Key == "3xc" ? "Triple Captain" : "Assistant Manager";
						Console.WriteLine($"{kv.Value.Count, 2} {chipName}:");

						// Which list of chip targets?  3xc or assman ?
						Dictionary<int,List<int>> chipTargets = kv.Key == "3xc" ? _fpl.Standings[_config.leagueId].ChipTarget3xc : _fpl.Standings[_config.leagueId].ChipTargetAss;
						foreach (var (targetId, fplManagers) in chipTargets) {
							Console.Write($"  - {fplManagers.Count} {_fpl.Bootstrap.GetElement(targetId).web_name} (");
							string glue = "";
							fplManagers.ForEach(entryId => {

								// Get the name of the manager, we could load this from a manager info file
								// but it's one less fetch per entry to just take it from the leaderboard

								Manager manager = _fpl.Managers[entryId];
								Result result = _fpl.Standings[_config.leagueId].standings.results.Find(r => r.entry == entryId);
								Console.Write($"{glue}{Utils.StandardiseName(result.player_name)}");
								glue = ", ";
							});
							Console.WriteLine(")");
						}
						Console.WriteLine("");
					}
				}
			}
		}


		void _reportCaptaincy() {
			_writeBlankLine();
			_writeHeader("Captaincy");
			_writeBlankLine();

			var list = _fpl.Standings[_config.leagueId].Captaincy.OrderByDescending(v => v.Value.Count);
			int totalManagers = _fpl.Managers.Count;	// this seems risky - what if we've loaded another league??  Test this league!

			foreach(var kv in list) {

				_sb.Length = 0;
				Element captain = _fpl.Bootstrap.GetElement(kv.Key);
				_sb.Append($"{kv.Value.Count, 4} {captain.web_name}");

				if (_config.incManagersInCaptaincy || kv.Value.Count == 1) {
					string glue = " (";

					kv.Value.ForEach(entryId => {
						Manager manager = _fpl.Managers[entryId];
						Result result = _fpl.Standings[_config.leagueId].standings.results.Find(r => r.entry == entryId);
						_sb.Append($"{glue}{Utils.StandardiseName(result.player_name)}");
						glue = ", ";
					});
					
					_sb.Append(")");
				}

				Console.WriteLine(_sb.ToString());
			}
		}	

		void _reportHits() {

			var list = _fpl.Managers.OrderByDescending(m => m.Value.GetTransferCost);

			int totalHitPoints = 0;
			int largestHitPoints = list.First().Value.GetTransferCost;
			int largestHitCount = 0;
			int noHitCount = 0;
			int rollCount = 0;

			foreach (var kv in list) {
				totalHitPoints += kv.Value.GetTransferCost;
				largestHitCount += kv.Value.GetTransferCost == largestHitPoints ? 1 : 0;
				noHitCount += kv.Value.GetTransferCost == 0 ? 1 : 0;
				rollCount += kv.Value.DidRoll ? 1 : 0;
			}
	
			Console.WriteLine($"\nAverage hit: {totalHitPoints / (float) list.Count():0.00} pts");	

			if (largestHitPoints != 0) {	
				Console.Write($"Biggest hit: {largestHitPoints}pts (");
				
				string glue = "";
				foreach (var kv in list) {
					if (kv.Value.GetTransferCost != largestHitPoints) {
						break;
					}

					Result result = _fpl.Standings[_config.leagueId].standings.results.Find(r => r.entry == kv.Value.GetEntryId);
					Console.Write(glue + Utils.StandardiseName(result.player_name));
					glue = ", ";
				}
				Console.WriteLine(")");
			}
			Console.WriteLine($"No hits:     {noHitCount} players");
			Console.WriteLine($"Rolled:      {rollCount} players");
		}


		private void _reportPoints() {
			_writeHeader("Points (net)", 12);
			
			int mostPoints = _fpl.Managers.Max(m => m.Value.GetNetPoints);
			Console.Write($"Highest: {mostPoints} pts");

			_fpl.Managers.Where(m => m.Value.GetNetPoints == mostPoints).ToList().ForEach(m => {
				var e = _standings.GetEntry(m.Value.GetEntryId);
				Console.Write(", "+Utils.StandardiseName(e.player_name));
			});

			Console.WriteLine(".");

			// Average

			double avgPoints = _fpl.Managers.Average(m => m.Value.GetNetPoints);
			Console.WriteLine($"Average: {avgPoints:0.00} pts");

			// Lowest

			Console.Write("Fewest : ");
			int lowestPoints = _fpl.Managers.Min(m => m.Value.GetNetPoints);
			Console.Write(lowestPoints+" pts");

			_fpl.Managers.Where(m => m.Value.GetNetPoints == lowestPoints).ToList().ForEach(m => {
				var e = _standings.GetEntry(m.Value.GetEntryId);
				Console.Write(", "+Utils.StandardiseName(e.player_name));
			});

			Console.WriteLine(".");
		}


		private void _reportBiggestRankMoves() { 
			//Console.Write("");
		}


		private void _reportCaptaincyReturns() {
			_writeBlankLine();
			_writeHeader("Captain Returns", 15);

			var captains = _fpl.Standings[_config.leagueId].Captaincy
				.Select(kv => new {
					Name = _fpl.Bootstrap.GetElement(kv.Key).web_name,
					Points = _fpl.Live.elements.Find(el => el.id == kv.Key).stats.total_points,
					Count = kv.Value.Count
				})
				.OrderByDescending(c => c.Count)
				.ThenByDescending(c => c.Points);

			foreach (var c in captains) {
				Console.WriteLine($"{c.Count,4} {c.Name}, {c.Points}pts");
			}
		}


		private void _reportPointsOnBench() {
			_writeBlankLine();
			_writeHeader("Bench", 7);

			double avgPoints = _fpl.Managers.Average(m => m.Value.GetBenchPoints);
			Console.WriteLine($"Avg : {avgPoints:0.00} pts");
			Console.Write("Most: ");
			
			int mostPoints = _fpl.Managers.Max(m => m.Value.GetBenchPoints);
			Console.Write(mostPoints+" pts");

			_fpl.Managers.Where(m => m.Value.GetBenchPoints == mostPoints).ToList().ForEach(m => {
				var e = _standings.GetEntry(m.Value.GetEntryId);
				Console.Write(", "+Utils.StandardiseName(e.player_name)+"\n");

				var man =_fpl.Managers[e.entry];
				List<int> bench = new();

				for (int i = 11; i <= 14; i++) {
					bench.Add(man.GetPicks.picks[i].element);
				};

				_expandBench(bench);	

			});
		}


		private void _reportTransferSummary() {
			_writeBlankLine();
			_writeHeader("Net transfer points", 20);
			
			int mostPoints = _fpl.Managers.Max(m => m.Value.GetTransfersResult());
			Console.WriteLine($"Best: {mostPoints} pts");

			var list = _fpl.Managers.Where(m => m.Value.GetTransfersResult() == mostPoints).ToList();
			if (list.Count == 1) {
				list.ForEach(m => {
					var e = _standings.GetEntry(m.Value.GetEntryId);
					Console.WriteLine("  "+Utils.StandardiseName(e.player_name)+":");
					
					var man =_fpl.Managers[e.entry];
					List<int> ins = new();
					List<int> outs = new();

					man.GetTransfers.Where(tr => tr.@event == _fpl.Bootstrap.GetCurrentGameweekId()).ToList().ForEach(tr => {
						ins.Add(tr.element_in);
						outs.Add(tr.element_out);
					});

					_expandTransfers(ins, outs, m.Value.GetTransferCost);	
				});
			} else {
				list.ForEach(m => {
					var e = _standings.GetEntry(m.Value.GetEntryId);
					Console.Write("\n  "+Utils.StandardiseName(e.player_name)+"\n");

					var man =_fpl.Managers[e.entry];
					List<int> ins = new();
					List<int> outs = new();

					man.GetTransfers.Where(tr => tr.@event == _fpl.Bootstrap.GetCurrentGameweekId()).ToList().ForEach(tr => {
						ins.Add(tr.element_in);
						outs.Add(tr.element_out);
					});

					_expandTransfers(ins, outs, m.Value.GetTransferCost);	
				});
			}

			// Lowest

			Console.Write("\nWorst: ");
			int lowestPoints = _fpl.Managers.Min(m => m.Value.GetTransfersResult());
			Console.Write(lowestPoints+" pts");

			list = _fpl.Managers.Where(m => m.Value.GetTransfersResult() == lowestPoints).ToList();
			
			if (list.Count == 1) {
				list.ForEach(m => {
					var e = _standings.GetEntry(m.Value.GetEntryId);
					Console.Write(", "+Utils.StandardiseName(e.player_name)+"\n");

					var man =_fpl.Managers[e.entry];
					List<int> ins = new();
					List<int> outs = new();

					man.GetTransfers.Where(tr => tr.@event == _fpl.Bootstrap.GetCurrentGameweekId()).ToList().ForEach(tr => {
						ins.Add(tr.element_in);
						outs.Add(tr.element_out);
					});

					_expandTransfers(ins, outs, m.Value.GetTransferCost);	
				});
			} else {
				list.ForEach(m => {
					var e = _standings.GetEntry(m.Value.GetEntryId);
					Console.Write("\n  "+Utils.StandardiseName(e.player_name)+"\n");

					var man =_fpl.Managers[e.entry];
				 List<int> ins = new();
					List<int> outs = new();

					man.GetTransfers.Where(tr => tr.@event == _fpl.Bootstrap.GetCurrentGameweekId()).ToList().ForEach(tr => {
						ins.Add(tr.element_in);
						outs.Add(tr.element_out);
					});

					_expandTransfers(ins, outs, m.Value.GetTransferCost);	
				});
			}
		}


		private void _reportTransferDetail() { 
			Console.Write("");
		}


		private void _reportMostPointsOnBenchTable() {
			_writeSectionBreak();
			_writeHeader("Season Points on Bench", 24);

			int placing = 0;
			int lastPoints = 0;
			int leagueBenchPoints = 0;

			_fpl.Managers.OrderByDescending(m => m.Value.SeasonPointsOnBench()).ToList().ForEach(manager => {

				leagueBenchPoints += manager.Value.SeasonPointsOnBench();

				if (++placing <= 10) {
					var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
					var delta = manager.Value.GetBenchPoints;
					var ds = _formatPointDelta(delta);

					// Only show placing number if not equal with previous
					bool equalWithLast = manager.Value.SeasonPointsOnBench() == lastPoints;
					lastPoints = manager.Value.SeasonPointsOnBench();
					var placingDisplay = equalWithLast ? " -- ": $"{Utils.ToOrdinal(placing)},";

					var suffix = string.IsNullOrEmpty(ds) ? "" : $" {ds}";
					Console.WriteLine($"{placingDisplay} {Utils.StandardiseName(name)}, {manager.Value.SeasonPointsOnBench()} pts{suffix}");
				}
			});

			// League average
			double average = (double)leagueBenchPoints / (double)_fpl.Managers.Count;
			_writeBlankLine();
			_writeLeagueAverage($"League average: {average.ToString("0.00")} pts");

		}


		private void _reportCaptaincyTable() {
			_writeSectionBreak();
			_writeHeader("Captaincy Leaderboard", 25);

			int placing = 0;
			int lastPoints = 0;
			int leagueCaptaincyPoints = 0;
			int totalManagers = _fpl.Managers.Count;

			var orderedManagers = _fpl.Managers.OrderByDescending(m => m.Value.CaptaincySeasonTally()).ToList();
			orderedManagers.ForEach(manager => {
				leagueCaptaincyPoints += manager.Value.CaptaincySeasonTally();
				if (++placing <= 10) {
					var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
					int delta = 0;
					string captainName = "none";
					int capId = manager.Value.GetCaptain;
					if (capId > 0) {
						FPLData.Instance.Elements.TryGetValue(capId, out ElementSummary el);
						ElementHistory eh = el.history.LastOrDefault();
						delta = (eh?.total_points ?? 0) * manager.Value.GetCaptainMultiplier;
						captainName = _fpl.Bootstrap.GetElement(capId).web_name;
					}
					var ds = _formatPointDelta(delta, $" {captainName}");
					bool equalWithLast = manager.Value.CaptaincySeasonTally() == lastPoints;
					lastPoints = manager.Value.CaptaincySeasonTally();
					var placingDisplay = equalWithLast ? " -- " : $"{Utils.ToOrdinal(placing)},";
					Console.WriteLine($"{placingDisplay} {Utils.StandardiseName(name)}, {manager.Value.CaptaincySeasonTally()} pts {ds}");
				}
			});

			// League average
			double average = (double)leagueCaptaincyPoints / (double)totalManagers;
			Console.WriteLine("...");
			_writeLeagueAverage($"League average: {average.ToString("0.00")} pts");
			Console.WriteLine("...");


			// Show bottom 5 managers by captaincy tally, with correct league placing
			placing = totalManagers - 4; // Start at 69th for 73 managers
			lastPoints = 0;
			var bottomManagers = _fpl.Managers.OrderBy(m => m.Value.CaptaincySeasonTally()).Take(5).ToList();
			bottomManagers.Reverse();
			bottomManagers.ForEach(manager => {
				var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
				int delta = 0;
				string captainName = "none";
				int capId = manager.Value.GetCaptain;
				if (capId > 0) {
					FPLData.Instance.Elements.TryGetValue(capId, out ElementSummary el);
					ElementHistory eh = el.history.LastOrDefault();
					delta = (eh?.total_points ?? 0) * manager.Value.GetCaptainMultiplier;
					captainName = _fpl.Bootstrap.GetElement(capId).web_name;
				}
				var ds = _formatPointDelta(delta, $" {captainName}");
				bool equalWithLast = manager.Value.CaptaincySeasonTally() == lastPoints;
				lastPoints = manager.Value.CaptaincySeasonTally();
				var placingDisplay = equalWithLast ? " --  " : $"{Utils.ToOrdinal(placing)},";
				Console.WriteLine($"{placingDisplay} {Utils.StandardiseName(name)}, {manager.Value.CaptaincySeasonTally()} pts {ds}");
				placing++;
			});
		}



		private void _reportMostHitsTable()
		{
			_writeSectionBreak();
			_writeHeader("Hits Leaderboard", 18);

			// Only include managers who have taken hits
			var managersWithHits = _fpl.Managers.Where(m => m.Value.SeasonHits() > 0)
				.OrderByDescending(m => m.Value.SeasonHits()).ToList();
			int totalWithHits = managersWithHits.Count;
			int leagueHitPoints = _fpl.Managers.Sum(m => m.Value.SeasonHits());

			// Top 10
			int placing = 0;
			int lastPoints = 0;
			managersWithHits.ForEach(manager => {
				if (++placing <= 10) {
					Result entry = _standings.GetEntry(manager.Value.GetEntryId);
					var name = entry.player_name;
					var tc = manager.Value.GetTransferCost;
					var tcs = _formatHitDelta(tc);
					bool equalWithLast = manager.Value.SeasonHits() == lastPoints;
					lastPoints = manager.Value.SeasonHits();
					var placingDisplay = equalWithLast ? " -- " : $"{Utils.ToOrdinal(placing)},";
					var suffix = string.IsNullOrEmpty(tcs) ? "" : $" {tcs}";
					Console.WriteLine($"{placingDisplay} {Utils.StandardiseName(name)}, -{manager.Value.SeasonHits()} pts{suffix}");
				}
			});

			// League average
			double average = (double)leagueHitPoints / (double)_fpl.Managers.Count;
			_writeBlankLine();
			_writeLeagueAverage($"League average: -{average.ToString("0.00")} pts");
		}


		private void _reportNoHitClub() {
			_writeSectionBreak();
			_writeHeader("No Hit Club", 11);

			var nhcList = _fpl.Managers.Where(m => m.Value.SeasonHits() == 0).ToList();

			if (nhcList.Count == 0) {
				Console.WriteLine("(none)");
			} else if (nhcList.Count > 9) {
				Console.WriteLine($"{nhcList.Count} managers");
			} else {
				nhcList.ForEach(manager => {
					var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
					Console.WriteLine($"{Utils.StandardiseName(name)}");
				});
			}
		}


		private void _reportBurnedTransfers() {
			int thisWeek = _fpl.Managers.Count(m => m.Value.BurnedTransferThisWeek());
			int totalBurned = _fpl.Managers.Sum(m => m.Value.SeasonBurnedTransfers());
			Console.WriteLine($"\nBurned transfers: {thisWeek} (total: {totalBurned})");
		}


		private void _reportHighestValueTeamTable() {
			_writeSectionBreak();
			_writeHeader("Team Value Leaderboard", 24);

			int totalManagers = _fpl.Managers.Count;
			var orderedManagers = _fpl.Managers.OrderByDescending(m => m.Value.GetCurrentTeamValue()).ToList();

			int placing = 0;
			int lastValue = 0;
			int leagueValue = 0;

			// Top 10
			orderedManagers.ForEach(manager => {
				leagueValue += manager.Value.GetCurrentTeamValue();

				if (++placing <= 10) {
					var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
					float v = manager.Value.GetCurrentTeamValue()/10f;
					bool equalWithLast = manager.Value.GetCurrentTeamValue() == lastValue;
					lastValue = manager.Value.GetCurrentTeamValue();
					var placingDisplay = equalWithLast ? " -- ": $"{Utils.ToOrdinal(placing)},";
					Console.WriteLine($"{placingDisplay} {Utils.StandardiseName(name)} (£{v:#.0}m)");
				}
			});

			// League average
			double average = (double)leagueValue / (double)totalManagers / 10d;
			Console.WriteLine("...");
			_writeLeagueAverage($"League average: £{average.ToString("0.0")}m");
			Console.WriteLine("...");

			// Bottom 5
			var bottomManagers = _fpl.Managers.OrderBy(m => m.Value.GetCurrentTeamValue()).Take(5).ToList();
			bottomManagers.Reverse();
			placing = totalManagers - 4;
			lastValue = 0;
			bottomManagers.ForEach(manager => {
				var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
				float v = manager.Value.GetCurrentTeamValue()/10f;
				bool equalWithLast = manager.Value.GetCurrentTeamValue() == lastValue;
				lastValue = manager.Value.GetCurrentTeamValue();
				var placingDisplay = equalWithLast ? " -- ": $"{Utils.ToOrdinal(placing)},";
				Console.WriteLine($"{placingDisplay} {Utils.StandardiseName(name)} (£{v:#.0}m)");
				placing++;
			});
		}


		private void _reportHighestScoresTable() {
			_writeSectionBreak();
			_writeHeader("Most Net Points in a GW", 28);

			List<(int, int, int)> np = new();

			foreach(var entry in _fpl.Managers) {
				np.AddRange(entry.Value.GetOrderedNetPoints());
			}

			np.Sort();
			np.Reverse();

			int totalEntries = np.Count;
			int totalPoints = np.Sum(e => e.Item1);

			// Top 10
			int placing = 0;
			int lastPoints = 0;
			np.ForEach(netPointEntry => {
				if (++placing <= 10) {
					var name = _standings.GetEntry(netPointEntry.Item3).player_name;
					bool equalWithLast = netPointEntry.Item1 == lastPoints;
					lastPoints = netPointEntry.Item1;
					var placingDisplay = equalWithLast ? " -- ": $"{Utils.ToOrdinal(placing)},";
					Console.WriteLine($"{placingDisplay} {Utils.StandardiseName(name)}, {netPointEntry.Item1} pts (GW{netPointEntry.Item2})");
				}
			});

			// League average
			double average = (double)totalPoints / (double)totalEntries;
			_writeBlankLine();
			_writeLeagueAverage($"League average: {average.ToString("0.00")} pts");
		}

		private void _reportTCLeaderboard() {
			_writeSectionBreak();
			_writeHeader("Triple Captain Leaderboard", 29);

			// Flatten all TC results from all managers into a single list
			var allTCResults = _fpl.Managers
				.SelectMany(m => m.Value.GetX3Results.Select(tc => new {
					ManagerId = m.Value.GetEntryId,
					Tally = tc.tally,
					Gw = tc.gw,
					Description = tc.description
				}))
				.OrderByDescending(tc => tc.Tally)
				.ThenBy(tc => tc.Gw)
				.ThenBy(tc => tc.Description)
				.ToList();

			if (allTCResults.Count == 0) {
				Console.WriteLine("\n(No triple captain chips played yet)");
				return;
			}

			// Calculate totals for average
			int totalTCPoints = allTCResults.Sum(tc => tc.Tally * 3);

			// Group by description to count managers per TC result
			var groupedResults = allTCResults
				.GroupBy(tc => new { tc.Tally, tc.Description })
				.OrderByDescending(g => g.Key.Tally)
				.ToList();

			// Show Leaderboard
			int placing = 0;
			int lastPoints = -1;
			int displayedRank = 0;

			foreach (var group in groupedResults) {
				placing += group.Count();

				// Update displayed rank only when points change
				bool sameAsLast = group.Key.Tally == lastPoints;
				if (!sameAsLast) {
					displayedRank = placing - group.Count() + 1;
					lastPoints = group.Key.Tally;
				}

				// Stop after 15th place (but show all tied at that position)
				if (displayedRank > 15) break;

				// Format: "1st, GW6 Haaland 48 pts, 5 managers" or "12th, GW13 Thiago 39 pts, Richard Pennystan"
				string managerText;
				if (group.Count() == 1) {
					var entry = _standings.GetEntry(group.First().ManagerId);
					managerText = Utils.StandardiseName(entry.player_name);
				} else {
					managerText = $"{group.Count()} managers";
				}

				// Strip parentheses from description
				var description = group.Key.Description.Trim('(', ')');
				var ordinal = displayedRank < 10 ? $" {Utils.ToOrdinal(displayedRank)}" : Utils.ToOrdinal(displayedRank);
				var placingDisplay = sameAsLast ? " --   " : $"{ordinal}, ";
				Console.WriteLine($"{placingDisplay}{description}, {managerText}");
			}

			// League average
			double average = (double)totalTCPoints / (double)allTCResults.Count;
			_writeBlankLine();
			_writeLeagueAverage($"League average: {average.ToString("0.00")} pts");
		}


		// private void _reportAMLeaderboard() {
		// 	Console.WriteLine("\n\nAssistant Manager Leaderboard");
		// 	Console.Write("-------------------------------");

		// 	int totalValidAMs = 0;
		// 	int totalAMPoints = 0;

		// 	// Order by descending but exclude TCs which are null
		// 	_fpl.Managers.OrderByDescending(m => m.Value.GetAmTally).ToList().ForEach(manager => {
				
		// 		if (manager.Value.GetAmTally == null) {
		// 			return;
		// 		}

		// 		totalValidAMs++;
		// 		totalAMPoints += (int) (manager.Value.GetAmTally);
		// 	});

		// 	// Show Leaderboard
		// 	int placing = 0;
		// 	int lastPoints = 0;
			
		// 	_fpl.Managers.OrderByDescending(m => m.Value.GetAmTally).ToList().ForEach(manager => {
				
		// 		if (manager.Value.GetAmTally == null) {
		// 			return;
		// 		}

		// 		if (++placing <= 10 || lastPoints == (int) manager.Value.GetAmTally) {
		// 			Result entry = _standings.GetEntry(manager.Value.GetEntryId);
		// 			var name = Utils.StandardiseName(entry.player_name);
		// 			int points = (int) manager.Value.GetAmTally;

		// 			// Only show placing number if not equal with previous
		// 			bool equalWithLast = points == lastPoints;
		// 			lastPoints = points;
					
		// 			if (!equalWithLast) { 
		// 				Console.WriteLine($"\n{Utils.ToOrdinal(placing)}, {points} pts\n - {name}");
		// 			} else {
		// 				Console.WriteLine($" - {name}");
		// 			}
		// 		}
		
		// 	});

		// 	// League average
		// 	double average = (double)totalAMPoints / (double)totalValidAMs;
			
		// 	Console.WriteLine($"\nLeague average: {average.ToString("0.00")} pts");
		// }



		public void _expandTransfers(List<int> ins, List<int> outs, int transferCost) {
			var glue = "";
			Console.Write("    Out: ");
			outs.ForEach(el => {
				int pts = _fpl.Live.elements.Find(e => e.id == el).stats.total_points;
				Console.Write($"{glue}{_fpl.Bootstrap.GetElement(el).web_name}({pts})");
				glue = ", ";
			});

			if (transferCost > 0) {
				var hitFormat = _isSocialMediaFormat ? $"hit [{transferCost}]" : $"hit ({transferCost})";
				Console.Write($", {hitFormat}");
			}

			Console.Write(".\n    In: ");
			glue = "";
			ins.ForEach(el => {
				int pts = _fpl.Live.elements.Find(e => e.id == el).stats.total_points;
				Console.Write($"{glue}{_fpl.Bootstrap.GetElement(el).web_name}({pts})");
				glue = ", ";
			});
			Console.WriteLine(".");
		}



		public void _expandBench(List<int> bench) {
			var glue = "";
			Console.Write("    ");
			bench.ForEach(el => {
				int pts = _fpl.Live.elements.Find(e => e.id == el).stats.total_points;
				Console.Write($"{glue}{_fpl.Bootstrap.GetElement(el).web_name}({pts})");
				glue = ", ";
			});
			Console.WriteLine(".");
		}


		// Social media formatting helpers

		bool _isSocialMediaFormat => _config.facebookFormat || _config.whatsappFormat;

		void _writeHeader(string title, int underlineLength = 0) {
			if (_config.facebookFormat) {
				Console.WriteLine(Utils.ToUnicodeBold(title));
			} else if (_config.whatsappFormat) {
				Console.WriteLine(Utils.ToWhatsAppBold(title));
			} else {
				Console.WriteLine(title);
				if (underlineLength > 0) {
					Console.WriteLine(new string('-', underlineLength));
				}
			}
		}

		void _writeSectionBreak(int newlines = 2) {
			if (_config.facebookFormat) {
				for (int i = 0; i < newlines - 1; i++) {
					Console.WriteLine(Utils.FacebookSpacer);
				}
			} else {
				Console.Write(new string('\n', newlines));
			}
		}

		// Write a blank line that Facebook won't collapse
		void _writeBlankLine() {
			Console.WriteLine(_config.facebookFormat ? Utils.FacebookSpacer : "");
		}

		// Write League average line with italic formatting for social media
		void _writeLeagueAverage(string text) {
			if (_config.facebookFormat) {
				Console.WriteLine(Utils.ToUnicodeItalic(text));
			} else if (_config.whatsappFormat) {
				Console.WriteLine(Utils.ToWhatsAppItalic(text));
			} else {
				Console.WriteLine(text);
			}
		}

		// Format hit cost delta - omit (=) for social media, use square brackets for negatives
		string _formatHitDelta(int transferCost) {
			if (transferCost == 0) {
				return _isSocialMediaFormat ? "" : "(=)";
			}
			return _isSocialMediaFormat ? $"[-{transferCost}]" : $"(-{transferCost})";
		}

		// Format point delta - omit (=) for social media, use square brackets for negatives
		string _formatPointDelta(int delta, string suffix = "") {
			if (delta == 0) {
				return _isSocialMediaFormat ? "" : $"(={suffix})";
			}
			string sign = delta > 0 ? "+" : "";
			if (_isSocialMediaFormat) {
				return $"[{sign}{delta}{suffix}]";
			}
			return $"({sign}{delta}{suffix})";
		}
	}
}