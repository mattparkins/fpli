using System.Collections.Generic;
using System.Text;

namespace fpli {
    public static class Engine {

		const string api = "https://fantasy.premierleague.com/api/";
		const string cachePath = ".cache/";
		static Config _config;
		static FPLData _fpl = new FPLData(cachePath, api);
		static StringBuilder _sb = new StringBuilder();
		
		public static async Task Execute(Config config) {
			
			// Configuration
			_config = config;
			Fetcher.Callrate = _config.callrate;

			// Initialise and load data
			_fpl.Init();
			await _fpl.Load(_config);
			_fpl.Preprocess();

			// Process intents that require the engine
			switch (config.intent) {
				case Intent.ExecuteMiniLeagueAnalysis:	_executeMiniLeagueAnalysis();	break;
			}
		}

		private static void _executeMiniLeagueAnalysis() {
			_reportChipUsage();
			_reportCaptaincy();
			_reportHits();

			// Points on bench ?
			// Transfer evaluation - should this be a post-week run ?
		}


		static void _reportChipUsage() {
			
			int totalChipsPlayed = 0;
			var list = _fpl.GetStandings.ChipUsage.OrderByDescending(v => v.Value.Count);
			
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

						Manager manager = _fpl.GetManager(entryId);
						Result result = _fpl.GetStandings.standings.results.Find(r => r.entry == entryId);
						Console.Write($"{glue}{result.player_name}");
						glue = ", ";
					});
					Console.WriteLine(")");
				}
			}
		}


		static void _reportCaptaincy() {
			Console.WriteLine("\nCaptaincy\n");

			var list = _fpl.GetStandings.Captaincy.OrderByDescending(v => v.Value.Count);
			int totalManagers = _fpl.GetManagersInScope.Count;

			foreach(var kv in list) {

				_sb.Length = 0;
				Element captain = _fpl.GetBootstrap.GetElement(kv.Key);
				_sb.Append($"{kv.Value.Count, 4} {captain.web_name}");

				if (_config.incManagersInCaptaincy) {
					string glue = ": ";

					kv.Value.ForEach(entryId => {
						Manager manager = _fpl.GetManager(entryId);
						Result result = _fpl.GetStandings.standings.results.Find(r => r.entry == entryId);
						_sb.Append($"{glue}{result.player_name}");
						glue = ", ";
					});
					
					_sb.Append(".");
				}

				Console.WriteLine(_sb.ToString());
			}
		}	

		static void _reportHits() {

			var list = _fpl.GetManagersInScope.OrderByDescending(m => m.GetTransferCost);

			int totalHitPoints = 0;
			int largestHitPoints = list.First().GetTransferCost;
			int largestHitCount = 0;
			int noHitCount = 0;
			int rollCount = 0;

			foreach (var kv in list) {
				totalHitPoints += kv.GetTransferCost;
				largestHitCount += kv.GetTransferCost == largestHitPoints ? 1 : 0;
				noHitCount += kv.GetTransferCost == 0 ? 1 : 0;
				rollCount += kv.DidRoll ? 1 : 0;
			}
	
			Console.WriteLine($"\nAverage hit: {totalHitPoints / (float) list.Count():0.00} pts");	

			if (largestHitPoints != 0) {	
				Console.Write($"Biggest hit: {largestHitPoints}pts (");
				
				string glue = "";
				foreach (var kv in list) {
					if (kv.GetTransferCost != largestHitPoints) {
						break;
					}

					Result result = _fpl.GetStandings.standings.results.Find(r => r.entry == kv.GetEntryId);
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