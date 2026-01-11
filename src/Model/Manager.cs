namespace fpli {
	public class Manager {
		int _entryId;
		Picks _picks;
		List<Picks> _picksHistory;
		List<Transfer> _transfers;
		int _captain;
		int _captainMultiplier;
		int _gw;
		int? _transfersResult = null;
		int? _amTally = null;	// Assistant manager tally - only non-null after the AM chip is complete
		List<(int tally, int gw, string description)> _x3Results = new();  // Triple captain results (supports multiple TC chips)
		int? _captaincySeasonTally = null; 	// Total points from captains/vc this season

		string _amConfig = "";

		ManagerHistory _managerHistory;
		List <(int, int, int)> _orderedNetPoints;
		
		public int GetEntryId 		{ get { return _entryId; }} 
		public int GetCaptain 		{ get { return _captain; }}
		public int GetCaptainMultiplier { get { return _captainMultiplier; }}
		public int GetNetPoints { get { return _picks.entry_history.points - GetTransferCost; } }
		public int GetBenchPoints	{ get { return _picks.entry_history.points_on_bench; }}
		public string GetChip 		{ get { return _picks.active_chip; }}
		public int GetTransferCount { get { return _picks.entry_history.event_transfers; }}
		public int GetTransferCost  { get { return _picks.entry_history.event_transfers_cost; }}
		public bool DidRoll			{ get { return GetChip == null && GetTransferCount == 0 && _gw != 1; }}
		public int? GetAmTally		{ get { return _amTally; }}
		public List<(int tally, int gw, string description)> GetX3Results { get { return _x3Results; }}
		public string GetAmConfig	{ get { return _amConfig; }}

		public Picks GetPicks { get { return _picks; } }
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

			_captain = 0;
			_picks.picks.ForEach(p => {
				if (p.is_captain && p.multiplier >= 2) {
					_captain = p.element;
					_captainMultiplier = p.multiplier;
				}
			});

			if (_captain == 0) {
				_picks.picks.ForEach(p => {
					if (p.is_vice_captain && p.multiplier >= 2) {
						_captain = p.element;
						_captainMultiplier = p.multiplier;
					}
				});
			}

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
		
		public int CaptaincySeasonTally() {
			if (_captaincySeasonTally == null) {
				_captaincySeasonTally = 0;
				int gameweekNumber = 1;

				// Iterate through _picksHistory and find the isCaptain pick, if the multiplier is zero then it was a VC
				_picksHistory.ForEach(gw => {

					Pick captain = null;

					// Find the captain pick
					gw.picks.ForEach(p => {
						if (p.is_captain && p.multiplier > 0) {
							captain = p;
						}
					});

					// If the captain has a zero multiplier then check for the VC
					if (captain == null) {
						gw.picks.ForEach(p => {
							if (p.is_vice_captain && p.multiplier > 0) {
								captain = p;
							}
						});
					}

					//if the captain is set (neither of the manager's C & VC might have played)
					//then add the multiplied sum to the tally
					if (captain != null) {
						FPLData.Instance.Elements.TryGetValue(captain.element, out ElementSummary el);
						el.history.FindAll(h => h.round == gameweekNumber)?.ForEach(h => _captaincySeasonTally += captain.multiplier * h.total_points);
					}

					gameweekNumber++;
				});	
			}

			return (int) _captaincySeasonTally;
		}

		public int SeasonTransfers()
		{
			int seasonTransfers = 0;

			_managerHistory.current.ForEach(gw =>
			{
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


		public int SeasonPointsOnBench()
		{
			int seasonPointsOnBench = 0;

			_managerHistory.current.ForEach(gw =>
			{
				seasonPointsOnBench += gw.points_on_bench;
			});

			return seasonPointsOnBench;
		}

		public int SeasonBurnedTransfers() {
			return CountBurnedTransfers(1, _managerHistory.current.Count);
		}

		public bool BurnedTransferThisWeek() {
			int currentGw = _managerHistory.current.Count;
			return CountBurnedTransfers(currentGw, currentGw) > 0;
		}

		private int CountBurnedTransfers(int fromGw, int toGw) {
			int burned = 0;
			int freeTransfers = 1;  // Start of season

			for (int i = 0; i < _managerHistory.current.Count; i++) {
				var gwHistory = _managerHistory.current[i];
				var gwPicks = _picksHistory[i];
				int gameweek = gwHistory.@event;
				int transfersMade = gwHistory.event_transfers;
				string chip = gwPicks.active_chip;

				// GW16 2024/25: everyone topped up to 5 FTs
				if (gameweek == 16) {
					freeTransfers = 5;
				}

				// WC/FH allow unlimited transfers, FTs carry through but no +1 gain
				if (chip == "wildcard" || chip == "freehit") {
					continue;
				}

				// Check for burn: had 5 FTs and made 0 transfers (only count if in range)
				if (freeTransfers == 5 && transfersMade == 0 && gameweek >= fromGw && gameweek <= toGw) {
					burned++;
				}

				// Calculate FTs for next week: use transfers, gain 1, cap at 5
				freeTransfers = Math.Min(5, freeTransfers - transfersMade + 1);
				if (freeTransfers < 1) freeTransfers = 1;  // Can't go below 1
			}

			return burned;
		}

		public int GetCurrentCashInBank() {
			return _managerHistory.current.Count > 0 ? _managerHistory.current.Last().bank : 0;
		}

		public int GetCurrentTeamValue() {
			return _managerHistory.current.Count > 0 ? _managerHistory.current.Last().value : 0;
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

			// Find the value of each 3x Chip. Managers can now have multiple TC chips per season.
			_managerHistory.chips.FindAll(ch => ch.name == "3xc").ForEach(ch => {

				int tally = 0;
				int gw = ch.@event;

				Console.WriteLine($"Manager history for {_entryId} 3xc played in {gw}, pick history count {_picksHistory.Count}");

				_picksHistory[gw - 1].picks.ForEach(p => {
					if (p.is_captain) {
						int elId = p.element;

						FPLData.Instance.Elements.TryGetValue(elId, out ElementSummary el);
						el.history.FindAll(h => h.round == gw)?.ForEach(h => tally += h.total_points ?? 0);

						string name = FPLData.Instance.Bootstrap.elements.Find(e => e.id == elId).web_name;
						string description = $"(GW{gw} {name} {tally * 3} pts)";
						_x3Results.Add((tally, gw, description));
					}
				});
			});
			

			// Assistant Manager chip - defunct, picks[15] no longer exists
			// _managerHistory.chips.FindAll(ch => ch.name == "manager").ForEach(ch => {
			// 	int gw = ch.@event;
			// 	if (gw + 2 <= _picksHistory.Count) {
			// 		_amTally = 0;
			// 		string mans = "(GW" + gw +": ";
			// 		string glue = "";
			// 		for (int i = 0; i < 3; i++) {
			// 			int amEl = _picksHistory[gw + i -1].picks[15].element;
			// 			FPLData.Instance.Elements.TryGetValue(amEl, out ElementSummary el);
			// 			el.history.FindAll(h => h.round == gw + i)?.ForEach(h => _amTally += h.total_points);
			// 			mans += $"{glue}{FPLData.Instance.Bootstrap.elements.Find(e => e.id == amEl).web_name}";
			// 			glue = ", ";
			// 			if (i >= 2) {
			// 				mans += ")";
			// 				Console.WriteLine($"Entry {_entryId}, Assistant Manager {mans} scored {_amTally} from GW{gw}");
			// 				_amConfig = mans;
			// 			}
			// 		}
			// 	}
			// });
		}
	}
}