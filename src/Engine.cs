namespace fpli {
    public static class Engine {

		const string api = "https://fantasy.premierleague.com/api/";
		const string cachePath = "data/";
		static Config _config;
		static FPLData _fpl = new FPLData(cachePath, api);
		
		public static async Task Execute(Config config) {
			
			// Configure
			_config = config;
			Fetcher.Callrate = _config.callrate;

			// Initialise
			_fpl.Init(_config);
			Analyser analyser = Analyser.Factory(config.intent, _fpl, _config);
			if (analyser == null) {
				Program.Quit($"No analyser available for intent: {config.intent}");
				return;
			}

			// Load
			await _fpl.PreFetch(true);
			await analyser.PreFetch();

			// Preprocess
			_fpl.Preprocess();
			analyser.Preprocess();

			analyser.Analyse();
		}
	}
}