using System.Security.Cryptography.X509Certificates;

namespace fpli {
	public class Manager {
		int _entryId;
		Picks _picks;
		List<Picks> _picksHistory;
		List<Transfer> _transfers;
		int _captain;
		int _gw;
		int? _transfersResult = null;
		int? _amTally = null;	// Assistant manager tally - only non-null after the AM chip is complete
		int? _x3Tally = null;	// Triple captain tally - the value of this manager's use of the TC chip

		string _x3Manager = "";
		string _amConfig = "";

		ManagerHistory _managerHistory;
		List <(int, int, int)> _orderedNetPoints;
		
		public int GetEntryId 		{ get { return _entryId; }} 
		public int GetCaptain 		{ get { return _captain; }}
		public int GetNetPoints		{ get { return _picks.entry_history.points - GetTransferCost; }}
		public int GetBenchPoints	{ get { return _picks.entry_history.points_on_bench; }}
		public string GetChip 		{ get { return _picks.active_chip; }}
		public int GetTransferCount { get { return _picks.entry_history.event_transfers; }}
		public int GetTransferCost  { get { return _picks.entry_history.event_transfers_cost; }}
		public bool DidRoll			{ get { return GetChip == null && GetTransferCount == 0 && _gw != 1; }}
		public int? GetAmTally		{ get { return _amTally; }}
		public int? GetX3Tally		{ get { return _x3Tally; }}
		public string GetX3Manager	{ get { return _x3Manager; }}
		public string GetAmConfig	{ get { return _amConfig; }}

		public Picks GetPicks 						{ get { return _picks; }}
		public List<Transfer> GetTransfers 			{ get { return _transfers; }}
		public ManagerHistory GetManagerHistory 	{ get { return _managerHistory; }}

		public Manager(int entryId) {
			_entryId = entryId;
		}

		public async Task Fetch(string cachePath, string api, int GW) {
			_gw = GW;
			_picks = await Fetcher.FetchAndDeserialise<Picks>($"{cachePath}picks_{_entryId}_GW{GW}.json", $"{api}entry/{_entryId}/event/{GW}/picks/", Utils.DaysAsSeconds(0.25f));
			_transfers = await Fetcher.FetchAndDeserialise<List<Transfer>>($"{cachePath}transfers_{_entryId}_GW{GW}.json", $"{api}entry/{_entryId}/transfers/", Utils.DaysAsSeconds(300));
			_managerHistory = await Fetcher.FetchAndDeserialise<ManagerHistory>($"{cachePath}entry_{_entryId}_history_GW{GW}.json", $"{api}entry/{_entryId}/history/", Utils.DaysAsSeconds(0.25f));

			// pull out any shortcut data
			_picks.picks.ForEach(p => {
				if (p.is_captain) {
					_captain = p.element;
				}
			});			

			_picksHistory = new List<Picks>();
			for (int gw = 1; gw <= GW; gw++) {
				int cacheExpiry = gw == GW ? Utils.DaysAsSeconds(0.25f) : Utils.DaysAsSeconds(300);
				_picksHistory.Add(await Fetcher.FetchAndDeserialise<Picks>($"{cachePath}picks_{_entryId}_GW{gw}.json", $"{api}entry/{_entryId}/event/{gw}/picks/", cacheExpiry));
			}

			// Calculate chips tallies
			CalculateChips();

		}

		public int GetTransfersResult() {
			if (_transfersResult == null) {
				_transfersResult = -GetTransferCost;

				if (_picks.active_chip != "freehit" && _picks.active_chip != "wildcard") {
					var live = FPLData.Instance.Live.elements;
					_transfers.Where(tr => tr.@event == _gw).ToList().ForEach(tr => {
						LiveElement elin = live.Find(el => el.id == tr.element_in);
						LiveElement elout = live.Find(el => el.id == tr.element_out);
						_transfersResult += elin.stats.total_points - elout.stats.total_points;
					});
				}
			}

			return (int) _transfersResult;
		}

		public int SeasonTransfers() {
			int seasonTransfers = 0;

			_managerHistory.current.ForEach(gw => {
				seasonTransfers += gw.event_transfers;
			});

			return seasonTransfers;
		}


		public int SeasonHits() {
			int seasonHits = 0;

			_managerHistory.current.ForEach(gw => {
				seasonHits += gw.event_transfers_cost;
			});
			
			return seasonHits;
		}


		public int SeasonPointsOnBench() {
			int seasonPointsOnBench = 0;

			_managerHistory.current.ForEach(gw => {
				seasonPointsOnBench += gw.points_on_bench;
			});
			
			return seasonPointsOnBench;
		}

		public int GetCurrentCashInBank() {
			return _managerHistory.current.Last().bank;
		}

		public int GetCurrentTeamValue() {
			return _managerHistory.current.Last().value;
		}

		// Returns a tuple of: net points, gameweek, entry id
		public List<(int, int, int)> GetOrderedNetPoints() {

			if (_orderedNetPoints == null) {
				_orderedNetPoints = new List<(int, int, int)>();

				_managerHistory.current.ForEach(
					hgw => _orderedNetPoints.Add(
						new(hgw.points - hgw.event_transfers_cost, hgw.@event, _entryId)
					)
				);

				_orderedNetPoints.Sort();
			}

			return _orderedNetPoints;
		}

		public void CalculateChips() {

			// Find the value of the 3x Chip.  First locate which week this manager used their 3xc chip
			// Then find the points scored by the captain that week, or leave as null if they haven't played 
			// their triple captain chip yet this season.

			_managerHistory.chips.FindAll(ch => ch.name == "3xc").ForEach(ch => {

				_x3Tally = 0;	// Set the tally from null to zero
				
				Console.WriteLine($"Manager history for {_entryId} 3xc played in {ch.@event}, pick history count {_picksHistory.Count}");

				_picksHistory[ch.@event -1].picks.ForEach(p => {
					if (p.is_captain) {
						int elId = p.element; 	// the id of the element

						FPLData.Instance.Elements.TryGetValue(elId, out ElementSummary el);
						el.history.FindAll(h => h.round == ch.@event)?.ForEach(h => _x3Tally += h.total_points);
						
						//Report result
						string name = FPLData.Instance.Bootstrap.elements.Find(e => e.id == elId).web_name;
						//Console.WriteLine($"Triple Captain: {name} scored {_x3Tally} points in GW{ch.@event}");
						_x3Manager = $"(GW{ch.@event} {name} {_x3Tally *3} pts)";
					}
				});
			});
			

			// Find the value of the AM chip.  First locate which week this manager used their AM chip
			// Then tally the points from the AM over the following 3 gameweeks, leave as null if we haven't 
			// reached the third gameweek.

			_managerHistory.chips.FindAll(ch => ch.name == "manager").ForEach(ch => {
				int gw = ch.@event;

				if (gw + 2 <= _picksHistory.Count) {	

					_amTally = 0;	// Set the tally from null to zero	
					string mans = "(GW" + gw +": "; 
					string glue = "";

					for (int i = 0; i < 3; i++) {

						// Get the manager name

						int amEl = _picksHistory[gw + i -1].picks[15].element;
						FPLData.Instance.Elements.TryGetValue(amEl, out ElementSummary el);
						el.history.FindAll(h => h.round == gw + i)?.ForEach(h => _amTally += h.total_points);
						mans += $"{glue}{FPLData.Instance.Bootstrap.elements.Find(e => e.id == amEl).web_name}";
						glue = ", ";

						//Report result after 3 gws
						if (i >= 2) {
							mans += ")";
							Console.WriteLine($"Entry {_entryId}, Assistant Manager {mans} scored {_amTally} from GW{gw}");
							_amConfig = mans;
						}
					}
				} else {
					//Console.WriteLine($"Entry {_entryId}, Incomplete assistant manager chip used in GW{gw}");
				}
			});
		}
	}
}