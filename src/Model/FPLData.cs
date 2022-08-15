using System.Text.Json;

namespace fpli {
	public class FPLData {

		public Bootstrap Bootstrap 							{ get; private set; }
		public EventStatus EventStatus 						{ get; private set; }
		public Dictionary<int, Manager> Managers			{ get; private set; } = new Dictionary<int, Manager>();			// entryid
		public Dictionary<int, LeagueStandings> Standings	{ get; private set; } = new Dictionary<int, LeagueStandings>();	// leagueid
		public Dictionary<int, List<Fixture>> Fixtures		{ get; private set; } = new Dictionary<int, List<Fixture>>();	// gameweek

		readonly string _cachePath;
		readonly string _api;
		
		Config _config;

		public FPLData(string cachePath, string api) {
			_cachePath = cachePath;
			_api = api;
		}


		public void Init(Config config) {
			_config = config;

			// Create cache folder if it doesn't exist 
			if (!Directory.Exists(_cachePath)) {
                Console.WriteLine($"Cache ({_cachePath}) does not exist, creating.");
                Directory.CreateDirectory(_cachePath);
            }
		}


		public async Task PreFetch() {
			Console.WriteLine("Reading & updating cache");
			            
			// Fetch Bootstrap, this will determine how we configure the cache for other items
			Bootstrap = await Fetcher.FetchAndDeserialise<Bootstrap>(_cachePath+"bootstrap.json", _api+"bootstrap-static/", Utils.HoursAsSeconds(1));
			EventStatus = await Fetcher.FetchAndDeserialise<EventStatus>(_cachePath+"event-status.json", _api+"event-status/", Utils.HoursAsSeconds(1));
		}


		public async Task LoadManager(int entryId) {
			Manager manager = new Manager(entryId);
			await manager.Fetch(_cachePath, _api, Bootstrap.GetCurrentGameweekId());
			Managers[entryId] = manager;
		}

		
		public async Task LoadLeague(int leagueId, int maxManagers) {
			Event gameweek = Bootstrap.GetCurrentGameweek();
			string GWStatusString = $"GW{Bootstrap.GetCurrentGameweekId()}_{EventStatus.GetSummaryStatus()}";

			// Leaderboard - use the gameweek + GWstatus + page in filename
			int finalPage = ((maxManagers -1) / 50) +1;
			string standingsFilename = $"standings_{leagueId}_{GWStatusString}";

			for (int page = 1; page <= finalPage; page++) {

				LeagueStandings leagueStandings = await Fetcher.FetchAndDeserialise<LeagueStandings>(
					$"{_cachePath}{standingsFilename}_{page}.json", 
					$"{_api}leagues-classic/{leagueId}/standings/?page_standings={page}&phase=1", 
					Utils.DaysAsSeconds(300));

				if (page == 1) {
					Standings[leagueId] = leagueStandings;
				} else {
					Standings[leagueId].standings.results.AddRange(leagueStandings.standings.results);
				}					
			}
		} 


		public async Task LoadFixtures(int gw) {

			// Set cache expiry as if the requested gameweek is in the future
			int cacheExpiry = Utils.DaysAsSeconds(7);
			string stat = "pre";

			if (gw < Bootstrap.GetCurrentGameweekId() || (gw == Bootstrap.GetCurrentGameweekId() && Bootstrap.GetCurrentGameweek().finished)) {

				// If the requested gameweek is in the past
				stat = "complete";
				cacheExpiry = Utils.DaysAsSeconds(30);

			} else if (gw == Bootstrap.GetCurrentGameweekId()) {
				
				// If the requested gameweek is current
				stat = EventStatus.GetSummaryStatus();
				cacheExpiry = Utils.HoursAsSeconds(1);
			}

			string filename = $"{_cachePath}fixtures_GW{gw}_{stat}.json";
			Fixtures[gw] = await Fetcher.FetchAndDeserialise<List<Fixture>>(filename, $"{_api}fixtures/?event={gw}", cacheExpiry);
		}


		// Process the loaded data, preparing it for use in any non-trivial analysers 
		public void Preprocess() {

			// League Standings preprocessing
			foreach (var kv in Standings) {
				kv.Value?.CalculateLeagueStats(this);
			}
		}
	}
}