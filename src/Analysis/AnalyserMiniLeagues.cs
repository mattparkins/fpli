namespace fpli {
	public class MiniLeagueAnalyser : Analyser {

		LeagueStandings _standings;

		public MiniLeagueAnalyser(FPLData fpl, Config config): base(fpl, config) {
			// Basic config sanity check
			if (_config.leagueId <= 0) {
				Program.Quit("LeagueId is invalid or not set");
			}

			if (_config.maxManagers <= 0) {
				Program.Quit("MaxManagers is invalid");
			}
		}

		public override async Task PreFetch() {
			
			// Load the league
			await _fpl.LoadLeague(_config.leagueId, _config.maxManagers);

			// Load each manager in the league
			foreach(var standingEntry in _fpl.Standings[_config.leagueId].standings.results) {
				await _fpl.LoadManager(standingEntry.entry);
			};
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

			Console.WriteLine("\n\n\nPostGW Analysis:\n");

			_reportPoints();
			_reportBiggestRankMoves();
			_reportPointsOnBench();
			_reportTransferSummary();
			_reportTransferDetail();
			_reportCaptaincyReturns();		


			Console.WriteLine("\n\nSeason stats:");

			_reportMostPointsOnBenchTable();	
			_reportHighestScoresTable();
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
				Console.WriteLine("\nNo Active Chips\n");
				return;
			}

			Console.WriteLine("\nActive Chips\n");

			foreach(var kv in list) {
				if (kv.Key != "none") {
					Console.Write($"{kv.Value.Count, 4} {kv.Key}: (");
					string glue = "";
					kv.Value.ForEach(entryId => {

						// Get the name of the manager, we could load this from a manager info file
						// but it's one less fetch per entry to just take it from the leaderboard

						Manager manager = _fpl.Managers[entryId];
						Result result = _fpl.Standings[_config.leagueId].standings.results.Find(r => r.entry == entryId);
						Console.Write($"{glue}{result.player_name}");
						glue = ", ";
					});
					Console.WriteLine(")");
				}
			}
		}


		void _reportCaptaincy() {
			Console.WriteLine("\nCaptaincy\n");

			var list = _fpl.Standings[_config.leagueId].Captaincy.OrderByDescending(v => v.Value.Count);
			int totalManagers = _fpl.Managers.Count;	// this seems risky - what if we've loaded another league??  Test this league!

			foreach(var kv in list) {

				_sb.Length = 0;
				Element captain = _fpl.Bootstrap.GetElement(kv.Key);
				_sb.Append($"{kv.Value.Count, 4} {captain.web_name}");

				if (_config.incManagersInCaptaincy) {
					string glue = ": ";

					kv.Value.ForEach(entryId => {
						Manager manager = _fpl.Managers[entryId];
						Result result = _fpl.Standings[_config.leagueId].standings.results.Find(r => r.entry == entryId);
						_sb.Append($"{glue}{result.player_name}");
						glue = ", ";
					});
					
					_sb.Append(".");
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
					Console.Write(glue + result.player_name);
					glue = ", ";
				}
				Console.WriteLine(")");
			}
			Console.WriteLine($"No hits:     {noHitCount} players");
			Console.WriteLine($"Rolled:      {rollCount} players");
		}


		private void _reportPoints() { 
			Console.WriteLine("Points (net)");
			Console.WriteLine("------------");
			
			int mostPoints = _fpl.Managers.Max(m => m.Value.GetNetPoints);
			Console.Write($"Highest: {mostPoints} pts");

			_fpl.Managers.Where(m => m.Value.GetNetPoints == mostPoints).ToList().ForEach(m => {
				var e = _standings.GetEntry(m.Value.GetEntryId);
				Console.Write(", "+e.player_name);
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
				Console.Write(", "+e.player_name);
			});

			Console.WriteLine(".");
		}


		private void _reportBiggestRankMoves() { 
			//Console.Write("");
		}


		private void _reportCaptaincyReturns() { 

			// Should really cover VCs

			int blanks = 0;
			int rets = 0;

			foreach(var kv in _fpl.Standings[_config.leagueId].Captaincy) {

				var captain = _fpl.Live.elements.Find(el => el.id == kv.Key);
				int points = captain.stats.total_points;

				if (points >= 4) {
					rets += kv.Value.Count;
				} else {
					blanks += kv.Value.Count;
				}
			}

			Console.WriteLine($"\nCaptains.");
			Console.WriteLine($"----------");
			Console.WriteLine($"Blanked : {blanks} manager{(blanks==1?"":"s")}");
			Console.WriteLine($"Returned: {rets} manager{(blanks==1?"":"s")}");
		}


		private void _reportPointsOnBench() { 

			Console.WriteLine("\nBench.");
			Console.WriteLine("-------");

			double avgPoints = _fpl.Managers.Average(m => m.Value.GetBenchPoints);
			Console.WriteLine($"Avg : {avgPoints:0.00} pts");
			Console.Write("Most: ");
			
			int mostPoints = _fpl.Managers.Max(m => m.Value.GetBenchPoints);
			Console.Write(mostPoints+" pts");

			_fpl.Managers.Where(m => m.Value.GetBenchPoints == mostPoints).ToList().ForEach(m => {
				var e = _standings.GetEntry(m.Value.GetEntryId);
				Console.Write(", "+e.player_name+"\n");

				var man =_fpl.Managers[e.entry];
				List<int> bench = new();

				for (int i = 11; i <= 14; i++) {
					bench.Add(man.GetPicks.picks[i].element);
				};

				_expandBench(bench);	

			});
		}


		private void _reportTransferSummary() { 
			Console.WriteLine("\nNet transfer points.");
			Console.WriteLine("--------------------");
			
			int mostPoints = _fpl.Managers.Max(m => m.Value.GetTransfersResult());
			Console.WriteLine($"Best: {mostPoints} pts");

			var list = _fpl.Managers.Where(m => m.Value.GetTransfersResult() == mostPoints).ToList();
			if (list.Count == 1) {
				list.ForEach(m => {
					var e = _standings.GetEntry(m.Value.GetEntryId);
					Console.WriteLine("  "+e.player_name+":");
					
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
					Console.Write("\n  "+e.player_name+"\n");

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
					Console.Write(", "+e.player_name+"\n");

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
					Console.Write("\n  "+e.player_name+"\n");

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
			Console.WriteLine("\n\nSeason Points on Bench");
			Console.WriteLine("------------------------");

			int placing = 0;
			int lastPoints = 0;

			_fpl.Managers.OrderByDescending(m => m.Value.SeasonPointsOnBench()).ToList().ForEach(manager => {
				if (++placing <= 5) {
					var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
					var delta = manager.Value.GetBenchPoints;
					var ds = delta > 0 ? $"+{delta}" : delta < 0 ? $"-{delta}" : "=";

					// Only show placing number if not equal with previous
					bool equalWithLast = manager.Value.SeasonPointsOnBench() == lastPoints;
					lastPoints = manager.Value.SeasonPointsOnBench();
					var placingDisplay = equalWithLast ? "  ": $"{placing}.";

					Console.WriteLine($"{placingDisplay} {name}, {manager.Value.SeasonPointsOnBench()} pts ({ds})");
				}
			});
		}	


		private void _reportMostHitsTable() {
			Console.WriteLine("\n\nHits Leaderboard");
			Console.WriteLine("------------------");

			int placing = 0;
			int lastPoints = 0;

			_fpl.Managers.OrderByDescending(m => m.Value.SeasonHits()).ToList().ForEach(manager => {
				if (++placing <= 5) {
					Result entry = _standings.GetEntry(manager.Value.GetEntryId);
					var name = entry.player_name;
					var tc = manager.Value.GetTransferCost;
					var tcs = tc == 0 ? "=" : "-"+tc;

					// Only show placing number if not equal with previous
					bool equalWithLast = manager.Value.SeasonHits() == lastPoints;
					lastPoints = manager.Value.SeasonHits();
					var placingDisplay = equalWithLast ? "  ": $"{placing}.";

					Console.WriteLine($"{placingDisplay} {name}, -{manager.Value.SeasonHits()} pts ({tcs})");
				}
			});
		}


		private void _reportNoHitClub() {
			Console.WriteLine("\n\nNo Hit Club");
			Console.WriteLine("-----------");

			var nhcList = _fpl.Managers.Where(m => m.Value.SeasonHits() == 0).ToList();

			if (nhcList.Count == 0) {
				Console.WriteLine("(none)");
			} else {
				nhcList.ForEach(manager => {
					var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
					Console.WriteLine($"{name}");
				});
			}
		}


		private void _reportHighestValueTeamTable() {
			Console.WriteLine("\n\nTeam Value Leaderboard");
			Console.WriteLine("------------------------");

			int placing = 0;
			int lastValue = 0;

			_fpl.Managers.OrderByDescending(m => m.Value.GetCurrentTeamValue()).ToList().ForEach(manager => {
				if (++placing <= 5) {
					var name = _standings.GetEntry(manager.Value.GetEntryId).player_name;
					float v = manager.Value.GetCurrentTeamValue()/10f;

					// Only show placing number if not equal with previous
					bool equalWithLast = manager.Value.GetCurrentTeamValue() == lastValue;
					lastValue = manager.Value.GetCurrentTeamValue();
					var placingDisplay = equalWithLast ? "  ": $"{placing}.";

					Console.WriteLine($"{placingDisplay} {name} (£{v:#.0}m)");
				}
			});
		}


		private void _reportHighestScoresTable() {
			Console.WriteLine("\n\nMost Net Points in a GW");
			Console.WriteLine("----------------------------");

			List<(int, int, int)> np = new();
			
			foreach(var entry in _fpl.Managers) {
				np.AddRange(entry.Value.GetOrderedNetPoints());
			}

			np.Sort();
			np.Reverse();

			int placing = 0;
			int lastPoints = 0;
			np.ForEach(netPointEntry => {
				if (++placing <= 5) {
					var name = _standings.GetEntry(netPointEntry.Item3).player_name;

					// Only show placing number if not equal with previous
					bool equalWithLast = netPointEntry.Item1 == lastPoints;
					lastPoints = netPointEntry.Item1;
					var placingDisplay = equalWithLast ? "  ": $"{placing}.";

					Console.WriteLine($"{placingDisplay} {name}, {netPointEntry.Item1} pts (GW{netPointEntry.Item2})");
				}
			});
		}




		public void _expandTransfers(List<int> ins, List<int> outs, int transferCost) {
			var glue = "";
			Console.Write("    Out: ");
			outs.ForEach(el => {
				int pts = _fpl.Live.elements.Find(e => e.id == el).stats.total_points;
				Console.Write($"{glue}{_fpl.Bootstrap.GetElement(el).web_name}({pts})");
				glue = ", ";
			});			

			if (transferCost > 0) {
				Console.Write($", hit ({transferCost})");
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
	}
}