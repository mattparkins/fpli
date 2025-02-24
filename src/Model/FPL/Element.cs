    
namespace fpli {
	public class ElementFixture
    {
        public int? id { get; set; }
        public int? code { get; set; }
        public int? team_h { get; set; }
        public object team_h_score { get; set; }
        public int? team_a { get; set; }
        public object team_a_score { get; set; }
        public int? @event { get; set; }
        public bool? finished { get; set; }
        public int? minutes { get; set; }
        public bool? provisional_start_time { get; set; }
        public DateTime? kickoff_time { get; set; }
        public string event_name { get; set; }
        public bool? is_home { get; set; }
        public int? difficulty { get; set; }
    }

    public class ElementHistory
    {
        public int? element { get; set; }
        public int? fixture { get; set; }
        public int? opponent_team { get; set; }
        public int? total_points { get; set; }
        public bool? was_home { get; set; }
        public DateTime? kickoff_time { get; set; }
        public int? team_h_score { get; set; }
        public int? team_a_score { get; set; }
        public int? round { get; set; }
        public bool? modified { get; set; }
        public int? minutes { get; set; }
        public int? goals_scored { get; set; }
        public int? assists { get; set; }
        public int? clean_sheets { get; set; }
        public int? goals_conceded { get; set; }
        public int? own_goals { get; set; }
        public int? penalties_saved { get; set; }
        public int? penalties_missed { get; set; }
        public int? yellow_cards { get; set; }
        public int? red_cards { get; set; }
        public int? saves { get; set; }
        public int? bonus { get; set; }
        public int? bps { get; set; }
        public string influence { get; set; }
        public string creativity { get; set; }
        public string threat { get; set; }
        public string ict_index { get; set; }
        public int? starts { get; set; }
        public string expected_goals { get; set; }
        public string expected_assists { get; set; }
        public string expected_goal_involvements { get; set; }
        public string expected_goals_conceded { get; set; }
        public int? mng_win { get; set; }
        public int? mng_draw { get; set; }
        public int? mng_loss { get; set; }
        public int? mng_underdog_win { get; set; }
        public int? mng_underdog_draw { get; set; }
        public int? mng_clean_sheets { get; set; }
        public int? mng_goals_scored { get; set; }
        public int? value { get; set; }
        public int? transfers_balance { get; set; }
        public int? selected { get; set; }
        public int? transfers_in { get; set; }
        public int? transfers_out { get; set; }
    }

    public class ElementHistoryPast
    {
        public string season_name { get; set; }
        public int? element_code { get; set; }
        public int? start_cost { get; set; }
        public int? end_cost { get; set; }
        public int? total_points { get; set; }
        public int? minutes { get; set; }
        public int? goals_scored { get; set; }
        public int? assists { get; set; }
        public int? clean_sheets { get; set; }
        public int? goals_conceded { get; set; }
        public int? own_goals { get; set; }
        public int? penalties_saved { get; set; }
        public int? penalties_missed { get; set; }
        public int? yellow_cards { get; set; }
        public int? red_cards { get; set; }
        public int? saves { get; set; }
        public int? bonus { get; set; }
        public int? bps { get; set; }
        public string influence { get; set; }
        public string creativity { get; set; }
        public string threat { get; set; }
        public string ict_index { get; set; }
        public int? starts { get; set; }
        public string expected_goals { get; set; }
        public string expected_assists { get; set; }
        public string expected_goal_involvements { get; set; }
        public string expected_goals_conceded { get; set; }
        public int? mng_win { get; set; }
        public int? mng_draw { get; set; }
        public int? mng_loss { get; set; }
        public int? mng_underdog_win { get; set; }
        public int? mng_underdog_draw { get; set; }
        public int? mng_clean_sheets { get; set; }
        public int? mng_goals_scored { get; set; }
    }

    public class ElementSummary
    {
        public List<ElementFixture> fixtures { get; set; }
        public List<ElementHistory> history { get; set; }
        public List<ElementHistoryPast> history_past { get; set; }
    }

}