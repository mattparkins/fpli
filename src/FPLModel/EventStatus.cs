using System.Linq;
using System.Text;

namespace fpli {

    public class Status
    {
        public bool bonus_added { get; set; }
        public string date { get; set; }
        public int @event { get; set; }
        public string points { get; set; } // l = live, p = provisional, r = confirmed
    }

	public class EventStatus
    {
        public List<Status> status { get; set; }
        public string leagues { get; set; }

        StringBuilder _sb = new StringBuilder();
        string _summaryStatus = null;

        // Create an identifier that can be used to uniquely identify the stages
        // that this file (within limits) can be.  Often used for the purpose of 
        // caching *other* files

        public string GetSummaryStatus() {
            if (_summaryStatus == null) {
                _sb.Length = 0;
                status.ForEach(st => {
                    _sb.Append(st.date.Substring(st.date.Length-2, 2));
                    _sb.Append(st.points);
                });
                _sb.Append("_");
                _sb.Append(leagues);
                _summaryStatus = _sb.ToString();
            }
            return _summaryStatus!;
        }

    }
}