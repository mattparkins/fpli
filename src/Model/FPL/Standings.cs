using System.Text.Json.Serialization;

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
        public int? admin_entry { get; set; }
        public int start_event { get; set; }
        public string code_privacy { get; set; }
        public bool has_cup { get; set; }
        public int? cup_league { get; set; }
        public string rank { get; set; }
    }

    public class NewEntries
    {
        public bool has_next { get; set; }
        public int page { get; set; }
        public List<NewEntry> results { get; set; } = new List<NewEntry>();
    }

    public class NewEntry
    {
        public int entry { get; set; }
        public string entry_name { get; set; }
        public DateTime? joined_time { get; set; }
        public string player_first_name { get; set; }
        public string player_last_name { get; set; }

        // The standings API has no player_name for new entries; compose it.
        public string PlayerName => $"{player_first_name} {player_last_name}".Trim();
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

        // True for rows synthesized from new_entries (manager still waiting to be
        // added to the league by FPL). Not part of the API payload.
        [JsonIgnore] public bool IsPending { get; set; }
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
        public DateTime? last_updated_data { get; set; }
        public League league { get; set; }
        public Standings standings { get; set; }

        public Dictionary<int,List<int>> Captaincy { get; private set; } = new Dictionary<int, List<int>>();        // elementId, list of entryIds
        public Dictionary<string,List<int>> ChipUsage { get; private set; } = new Dictionary<string, List<int>>();  // chipType, list of entryIds
        public Dictionary<int,List<int>> ChipTarget3xc { get; private set; } = new Dictionary<int, List<int>>();       // elementId, list of entryIds - for triple captain
        public Dictionary<int,List<int>> ChipTargetAss { get; private set; } = new Dictionary<int, List<int>>();       // elementId, list of entryIds - for assistant manager

        public void CalculateLeagueStats(FPLData fpl) {
            _calculateCaptaincy(fpl);
            _calculateChipUsage(fpl);
        }

        public Result GetEntry(int entryId) {
            return standings.results.Find(r => r.entry == entryId);
        }

        // Count of managers merged in from new_entries who are still pending
        // addition to the league by FPL.
        public int PendingCount => standings?.results?.Count(r => r.IsPending) ?? 0;

        private void _calculateCaptaincy(FPLData fpl) {
            standings.results.ForEach(r => {
                Manager manager = fpl.Managers[r.entry];
                if (!Captaincy.ContainsKey(manager.GetCaptain)) {
                    Captaincy[manager.GetCaptain] = new List<int>();
                }
                Captaincy[manager.GetCaptain].Add(manager.GetEntryId);
            });
        }

        private void _calculateChipUsage(FPLData fpl) {
            standings.results.ForEach(r => {
                Manager manager = fpl.Managers[r.entry];
                string chip = manager.GetChip ?? "none";
                if (!ChipUsage.ContainsKey(chip)) {
                    ChipUsage[chip] = new List<int>();
                }
                ChipUsage[chip].Add(manager.GetEntryId);

                // if the chip is triple captain or assistant manager then we need to know who the target was
                if (chip == "3xc") {
                    if (!ChipTarget3xc.ContainsKey(manager.GetCaptain)) {
                        ChipTarget3xc[manager.GetCaptain] = new List<int>();
                    }
                    ChipTarget3xc[manager.GetCaptain].Add(manager.GetEntryId);
                }

                // Assistant Manager chip - defunct, picks[15] no longer exists
                // if (chip == "manager") {
                //     int managerElID = manager.GetPicks.picks[15].element;
                //     if (!ChipTargetAss.ContainsKey(managerElID)) {
                //         ChipTargetAss[managerElID] = new List<int>();
                //     }
                //     ChipTargetAss[managerElID].Add(manager.GetEntryId);
                // }
            });
        }
    }
}