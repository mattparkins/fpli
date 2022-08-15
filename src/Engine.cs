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

			// Load
			await _fpl.PreFetch(analyser.RequiresHistory);
			await analyser.PreFetch();

			// Preprocess
			_fpl.Preprocess();
			analyser.Preprocess();

			analyser.Analyse();
		}
	}
}