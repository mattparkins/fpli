using System.Text;

namespace fpli {
	public abstract class Analyser {

		protected FPLData _fpl;
		protected Config _config;
		protected static StringBuilder _sb = new StringBuilder();

		public Analyser(FPLData fpl, Config config) {
			_config = config;
			_fpl = fpl;
		}

		public abstract Task PreFetch();
		public abstract void Preprocess();
		public abstract void Analyse();

		public virtual bool RequiresHistory { get { return false; } }

		public static Analyser Factory(Intent intent, FPLData fpl, Config config) {
			switch (intent) {
				case Intent.ExecuteMiniLeagueAnalysis:	return new MiniLeagueAnalyser(fpl, config);
				case Intent.ExecuteFixtureAnalysis:		return new FixtureAnalyser(fpl, config);
			}
			return null;
		}
	}
}