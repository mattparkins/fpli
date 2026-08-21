using System.Text.Json;

namespace fpli {
	public class FPLData {

		public Bootstrap Bootstrap 							{ get; private set; }
		public EventStatus EventStatus 						{ get; private set; }
		public Live Live									{ get; private set; }	// Current gameweek Live data
		public Dictionary<int, Manager> Managers			{ get; private set; } = new Dictionary<int, Manager>();			// entryid
		public Dictionary<int, LeagueStandings> Standings	{ get; private set; } = new Dictionary<int, LeagueStandings>();	// leagueid
		public Dictionary<int, List<Fixture>> Fixtures		{ get; private set; } = new Dictionary<int, List<Fixture>>();	// gameweek
		public Dictionary<int, ElementSummary> Elements		{ get; private set; } = new Dictionary<int, ElementSummary>();	// elementid
		public History History 								{ get; private set; } = new History();

		readonly string _dataPath;
		readonly string _cachePath;
		readonly string _api;

		public static FPLData Instance 						{ get; private set; }
		
		Config _config;

		public FPLData(string dataPath, string api) {
			_cachePath = dataPath + ".cache/";
			_dataPath = dataPath;
			_api = api;
			if (Instance != null) {
				Console.WriteLine("Warning: FPLData instance already exists, overwriting.");
			}
			Instance = this;
		}

		public void Init(Config config) {
			_config = config;

			// Create cache folder if it doesn't exist 
			if (!Directory.Exists(_cachePath)) {
                Console.WriteLine($"Cache ({_cachePath}) does not exist, creating.");
                Directory.CreateDirectory(_cachePath);
            }
		}


		public async Task PreFetch(bool loadHistory) {

			Console.WriteLine("Updating cache");

			// Fetch Bootstrap, this will determine how we configure the cache for other items
			Bootstrap = await Fetcher.FetchAndDeserialise<Bootstrap>(_cachePath+"bootstrap.json", _api+"bootstrap-static/", Utils.HoursAsSeconds(1));
			EventStatus = await Fetcher.FetchAndDeserialise<EventStatus>(_cachePath+"event-status.json", _api+"event-status/", Utils.HoursAsSeconds(1));

			if (loadHistory) {
				Console.WriteLine("Reading historic FPL data");
				History.Load(_dataPath);

				// Sync completed gameweeks for current season to historic folder
				await History.SyncCurrentSeason(Bootstrap, _api, _cachePath);
			}
			
			int gw = Bootstrap.GetCurrentGameweekId();
			if (gw < 1)
			{
				Console.WriteLine("Currently in pre-seaason, skipping live data & players");
			}
			else
			{
				Live = await Fetcher.FetchAndDeserialise<Live>(_cachePath + "live_GW" + gw + ".json", _api + "event/" + gw + "/live/", Utils.HoursAsSeconds(1));
				
				// Fetch element summaries
				// Iterate through each element identified in the bootstrap
				// Fetch & store each element summary

				foreach (Element element in Bootstrap.elements) {
					ElementSummary elementSummary = await Fetcher.FetchAndDeserialise<ElementSummary>(
						$"{_cachePath}element_summary_{element.id}.json",
						$"{_api}element-summary/{element.id}/",
						Utils.HoursAsSeconds(1));
					Elements[element.id] = elementSummary;
				}
			}


		}

		public async Task<bool> LoadManager(int entryId) {
			Manager manager = new Manager(entryId);
			await manager.Fetch(_cachePath, _api, Bootstrap.GetCurrentGameweekId());

			// A pending manager (or one who joined mid-season) may have no picks for
			// the current gameweek yet; the picks endpoint 404s and Fetch yields null.
			// Skip them so downstream sections never dereference a null manager. Warn
			// on stderr so stdout (copied into WhatsApp/Facebook) stays clean.
			if (manager.GetPicks == null) {
				Console.Error.WriteLine($"warning: no GW{Bootstrap.GetCurrentGameweekId()} picks for entry {entryId} - skipped");
				return false;
			}

			Managers[entryId] = manager;
			return true;
		}

		
		public async Task LoadLeague(int leagueId, int maxManagers) {
			Event gameweek = Bootstrap.GetCurrentGameweek();
			string GWStatusString = $"GW{Bootstrap.GetCurrentGameweekId()}_{EventStatus.GetSummaryStatus()}";

			// Leaderboard - use the gameweek + GWstatus + page in filename
			int finalPage = ((maxManagers -1) / 50) +1;
			string standingsFilename = $"standings_{leagueId}_{GWStatusString}";

			// After the GW1 deadline (and until FPL processes the league overnight),
			// managers who have joined a mini league sit in the standings API's
			// new_entries block rather than in standings.results. Fetch that block first
			// so those pending managers can be included. It is transient (empties once
			// the league is processed), so cache it for only 1 hour.
			List<NewEntry> pending = new List<NewEntry>();
			for (int page = 1; ; page++) {
				LeagueStandings newEntriesPage = await Fetcher.FetchAndDeserialise<LeagueStandings>(
					$"{_cachePath}{standingsFilename}_new_entries_{page}.json",
					$"{_api}leagues-classic/{leagueId}/standings/?page_new_entries={page}&phase=1",
					Utils.HoursAsSeconds(1));

				List<NewEntry> pageResults = newEntriesPage?.new_entries?.results;
				int added = pageResults?.Count ?? 0;
				if (added > 0) {
					pending.AddRange(pageResults);
				}

				// Stop at the last page, once we hit the cap, or if a page comes back empty
				// (guards a malformed has_next=true with no results from looping forever).
				bool hasNextNewEntries = newEntriesPage?.new_entries?.has_next ?? false;
				if (!hasNextNewEntries || pending.Count >= maxManagers || added == 0) {
					break;
				}
			}

			// While managers are pending, standings.results is an incomplete snapshot.
			// If it were written under the normal 300-day cache name, tomorrow's run
			// (after FPL has processed the league, when new_entries is empty again)
			// would serve the stale, incomplete standings and the pending managers would
			// vanish. So while the league is in flux, cache the standings under a distinct
			// _pending_ name with a 1-hour TTL instead of 300 days.
			bool leagueInFlux = pending.Count > 0;

			for (int page = 1; page <= finalPage; page++) {

				string standingsCacheFile = leagueInFlux
					? $"{_cachePath}{standingsFilename}_pending_{page}.json"
					: $"{_cachePath}{standingsFilename}_{page}.json";
				int standingsTtl = leagueInFlux ? Utils.HoursAsSeconds(1) : Utils.DaysAsSeconds(300);

				LeagueStandings leagueStandings = await Fetcher.FetchAndDeserialise<LeagueStandings>(
					standingsCacheFile,
					$"{_api}leagues-classic/{leagueId}/standings/?page_standings={page}&phase=1",
					standingsTtl);

				if (page == 1) {
					Standings[leagueId] = leagueStandings;
				} else {
					Standings[leagueId].standings.results.AddRange(leagueStandings.standings.results);
				}

				// No more standings pages? Stop early.
				if (!(leagueStandings.standings?.has_next ?? false)) {
					break;
				}
			}

			// Store the full pending list, then merge any pending manager not already
			// present into standings.results (up to maxManagers). Merged rows are flagged
			// IsPending and flow through GetEntry, captaincy, chip usage and every
			// analyser section unchanged.
			LeagueStandings standings = Standings[leagueId];
			standings.new_entries ??= new NewEntries();
			standings.new_entries.results = pending;

			foreach (NewEntry e in pending) {
				if (standings.standings.results.Count >= maxManagers) {
					break;
				}
				if (standings.standings.results.Any(r => r.entry == e.entry)) {
					continue;
				}
				standings.standings.results.Add(new Result {
					entry = e.entry,
					entry_name = e.entry_name,
					player_name = e.PlayerName,
					IsPending = true
				});
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