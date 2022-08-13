namespace fpli {
	public class MiniLeagueAnalyser : Analyser {

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


		public override void Preprocess() {

			// Pre-GW1, not sure what to do, not mini-league analysis thats's for sure.
			if (_fpl.Bootstrap.GetCurrentGameweekId() <= 0) {
				Program.Quit("Cannot execute Mini-League Analysis if GW is <= 0");
			}
		}


		public override void Analyse() {
			_reportChipUsage();
			_reportCaptaincy();
			_reportHits();

			// Points on bench ?
			// Transfer evaluation - should this be a post-week run ?
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
	}
}