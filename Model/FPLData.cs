using System.Text.Json;

namespace fpli {
	public class FPLData {

		Bootstrap _bootstrap = new Bootstrap();
		EventStatus _eventStatus = new EventStatus();
		List<Manager> _managers = new List<Manager>();
		LeagueStandings _standings = new LeagueStandings();

		public Bootstrap GetBootstrap { get { return _bootstrap; }}
		public EventStatus GetEventStatus { get { return _eventStatus; }}
		public List<Manager> GetManagersInScope { get { return _managers; }}
		public LeagueStandings GetStandings { get { return _standings; }}

		readonly string _cachePath;
		readonly string _api;

		public FPLData(string cachePath, string api) {
			_cachePath = cachePath;
			_api = api;
		}

		public void Init() {

			// Create cache folder if it doesn't exist 
			if (!Directory.Exists(_cachePath)) {
                Console.WriteLine($"Cache ({_cachePath}) does not exist, creating.");
                Directory.CreateDirectory(_cachePath);
            }
		}


		public async Task Load(Config config) {
			            
			// Fetch Bootstrap, this will determine how we configure the cache for other items
			_bootstrap = await Fetcher.FetchAndDeserialise<Bootstrap>(_cachePath+"bootstrap.json", _api+"bootstrap-static/", Utils.HoursAsSeconds(1));
			_eventStatus = await Fetcher.FetchAndDeserialise<EventStatus>(_cachePath+"event-status.json", _api+"event-status/", Utils.HoursAsSeconds(1));

			Event gameweek = _bootstrap.GetCurrentGameweek();
			string GWStatusString = $"GW{_bootstrap.GetCurrentGameweekId()}_{_eventStatus.GetSummaryStatus()}";

			// Fetch required files for select intent type

			if (config.intent == Intent.ExecuteMiniLeagueAnalysis) {

				// Pre-GW1, not sure what to do, not mini-league analysis thats's for sure.
				
				if (_bootstrap.GetCurrentGameweekId() <= 0) {
					Program.Quit("Cannot execute Mini-League Analysis if GW is <= 0");
				}

				// Leaderboard - use the gameweek + GWstatus + page in filename
				int finalPage = ((config.maxManagers -1) / 50) +1;
				string standingsFilename = $"standings_{config.leagueId}_{GWStatusString}";

				for (int page = 1; page <= finalPage; page++) {

					LeagueStandings leagueStandings = await Fetcher.FetchAndDeserialise<LeagueStandings>(
						$"{_cachePath}{standingsFilename}_{page}.json", 
						$"{_api}leagues-classic/{config.leagueId}/standings/?page_standings={page}&phase=1", 
						Utils.DaysAsSeconds(300));

					if (page == 1) {
						_standings = leagueStandings;
					} else {
						_standings.standings.results.AddRange(leagueStandings.standings.results);
					}					
				}

				// Manager data, Picks and transfers - current GW in filename
				foreach(var standingEntry in _standings.standings.results) {
					Manager manager = new Manager(standingEntry.entry);
					await manager.Fetch(_cachePath, _api, _bootstrap.GetCurrentGameweekId());
					_managers.Add(manager);
				};
			}
		}


		// Process the loaded data, preparing it for use in any non-trivial analysers 

		public void Preprocess() {
			_standings?.CalculateLeagueStats(this);
		}


		public Manager GetManager(int entryId) {
			return _managers.Find(m => m.GetEntryId == entryId);
		}
	}
}