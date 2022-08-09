using System.Text;

namespace fpli {
    public static class Engine {

		const string api = "https://fantasy.premierleague.com/api/";
		const string cachePath = ".cache/";
		static Config _config = new Config();
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
			_reportTransfers();
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
					Console.Write($"{kv.Value.Count, 4} {kv.Key}: ");
					string glue = "";
					kv.Value.ForEach(entryId => {

						// Get the name of the manager, we could load this from a manager info file
						// but it's one less fetch per entry to just take it from the leaderboard

						Manager manager = _fpl.GetManager(entryId);
						Result result = _fpl.GetStandings.standings.results.Find(r => r.entry == entryId);
						Console.Write($"{glue}{result.player_name}");
						glue = ", ";
					});
					Console.WriteLine(".");
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


		static void _reportTransfers() {	
			// Transfer summary: Average hits, total rolls, total lost transfers
			// By player in/out
			// By manager, inc lost transfers, ordered by net points
		}
	}
}