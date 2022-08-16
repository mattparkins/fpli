namespace fpli {
	public class Manager {
		int _entryId;
		Picks _picks;
		List<Transfer> _transfers;
		int _captain;
		int _gw;
		int? _transfersResult = null;
		
		public int GetEntryId 		{ get { return _entryId; }} 
		public int GetCaptain 		{ get { return _captain; }}
		public int GetNetPoints		{ get { return _picks.entry_history.points - GetTransferCost; }}
		public int GetBenchPoints	{ get { return _picks.entry_history.points_on_bench; }}
		public string GetChip 		{ get { return _picks.active_chip; }}
		public int GetTransferCount { get { return _picks.entry_history.event_transfers; }}
		public int GetTransferCost  { get { return _picks.entry_history.event_transfers_cost; }}
		public bool DidRoll			{ get { return GetChip == null && GetTransferCount == 0 && _gw != 1; }}

		public Picks GetPicks 		{ get { return _picks; }}
		public List<Transfer> GetTransfers { get { return _transfers; }}

		public Manager(int entryId) {
			_entryId = entryId;
		}

		public async Task Fetch(string cachePath, string api, int GW) {
			_gw = GW;
			_picks = await Fetcher.FetchAndDeserialise<Picks>($"{cachePath}picks_{_entryId}_GW{GW}.json", $"{api}entry/{_entryId}/event/{GW}/picks/", Utils.DaysAsSeconds(1));
			_transfers = await Fetcher.FetchAndDeserialise<List<Transfer>>($"{cachePath}transfers_{_entryId}_GW{GW}.json", $"{api}entry/{_entryId}/transfers/", Utils.DaysAsSeconds(300));

			// pull out any shortcut data
			_picks.picks.ForEach(p => {
				if (p.is_captain) {
					_captain = p.element;
				}
			});
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
	}
}