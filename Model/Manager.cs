namespace fpli {
	public class Manager {
		int _entryId;
		Picks _picks;
		List<Transfer> _transfers;
		int _captain;
		int _gw;

		public int GetEntryId 		{ get { return _entryId; }} 
		public int GetCaptain 		{ get { return _captain; }}
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
			_picks = await Fetcher.FetchAndDeserialise<Picks>($"{cachePath}picks_{_entryId}_GW{GW}.json", $"{api}entry/{_entryId}/event/{GW}/picks/", Utils.DaysAsSeconds(300));
			_transfers = await Fetcher.FetchAndDeserialise<List<Transfer>>($"{cachePath}transfers_{_entryId}_GW{GW}.json", $"{api}entry/{_entryId}/transfers/", Utils.DaysAsSeconds(300));

			// pull out any shortcut data
			_picks.picks.ForEach(p => {
				if (p.is_captain) {
					_captain = p.element;
				}
			});
		}
	}
}