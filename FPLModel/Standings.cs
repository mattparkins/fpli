namespace fpli {
    public class League
    {
        public int id { get; set; }
        public string name { get; set; }
        public DateTime created { get; set; }
        public bool closed { get; set; }
        public string max_entries { get; set; }
        public string league_type { get; set; }
        public string scoring { get; set; }
        public int admin_entry { get; set; }
        public int start_event { get; set; }
        public string code_privacy { get; set; }
        public bool has_cup { get; set; }
        public string cup_league { get; set; }
        public string rank { get; set; }
    }

    public class NewEntries
    {
        public bool has_next { get; set; }
        public int page { get; set; }
        public List<string> results { get; set; }
    }

    public class Result
    {
        public int id { get; set; }
        public int event_total { get; set; }
        public string player_name { get; set; }
        public int rank { get; set; }
        public int last_rank { get; set; }
        public int rank_sort { get; set; }
        public int total { get; set; }
        public int entry { get; set; }
        public string entry_name { get; set; }
    }

    public class Standings
    {
        public bool has_next { get; set; }
        public int page { get; set; }
        public List<Result> results { get; set; }
    }

	public class LeagueStandings
    {
        public NewEntries new_entries { get; set; }
        public DateTime last_updated_data { get; set; }
        public League league { get; set; }
        public Standings standings { get; set; }

        public Dictionary<int,List<int>> Captaincy { get; private set; } = new Dictionary<int, List<int>>();        // elementId, list of entryIds
        public Dictionary<string,List<int>> ChipUsage { get; private set; } = new Dictionary<string, List<int>>();  // chipType, list of entryIds

        FPLData _fpl;

        public void CalculateLeagueStats(FPLData fpl) {
            _fpl = fpl;
            _calculateCaptaincy();
            _calculateChipUsage();
        }

        private void _calculateCaptaincy() {
            standings.results.ForEach(r => {
                Manager manager = _fpl.GetManagersInScope.Find(m => m.GetEntryId == r.entry);
                if (!Captaincy.ContainsKey(manager.GetCaptain)) {
                    Captaincy[manager.GetCaptain] = new List<int>();
                }
                Captaincy[manager.GetCaptain].Add(manager.GetEntryId);
            });
        }

        private void _calculateChipUsage() {
            standings.results.ForEach(r => {
                Manager manager = _fpl.GetManagersInScope.Find(m => m.GetEntryId == r.entry);
                string chip = manager.GetChip ?? "none";
                if (!ChipUsage.ContainsKey(chip)) {
                    ChipUsage[chip] = new List<int>();
                }
                ChipUsage[chip].Add(manager.GetEntryId);
            });
        }
    }
}