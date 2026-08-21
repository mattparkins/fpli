using System.Net;
using System.Text.Json;

namespace fpli {
	public static class Fetcher {

		static DateTime _nextFetchStamp = DateTime.MinValue;
        static HttpClient _client = new HttpClient();
        
        public static float Callrate { get; set; } = 1f;   // Maximum calls per second

		public static async Task<T> FetchAndDeserialise<T>(string filename, string endpoint, int cacheExpiryInSeconds, bool tolerateNotFound = false) {
			string text = await Fetcher.Fetch(filename, endpoint, cacheExpiryInSeconds, tolerateNotFound);
			if (text == null) {
				return default;
			}

			try {
				return JsonSerializer.Deserialize<T>(text, Utils.JSONConfig);
			} catch (JsonException e) {
				// A schema change (e.g. a field that used to be an int arriving as null)
				// should fail with a readable message, not an unhandled exception.
				Console.WriteLine($"error: could not parse {filename}: {e.Message}");
				Environment.Exit(-1);
				return default;  // Unreachable but required for compiler
			}
		}

		public static async Task<string> Fetch(string filename, string uri, int cacheExpiryInSeconds, bool tolerateNotFound = false) {

            // Convert uri to a filename
            string json = "";

            // If the file exists, and isn't past its expiry then we can try loading a deserialising it
            if (File.Exists(filename)) {

                // Check age - has it expired?
                DateTime lastWriteTime = File.GetLastWriteTimeUtc(filename);
                DateTime expiry = lastWriteTime.AddSeconds(cacheExpiryInSeconds);

                if (expiry > DateTime.UtcNow) {
                
                    json = File.ReadAllText(filename);

                    // If the object isn't null then return it
                    if (json.Length >= 2) {
                        return json;
                    } 
                }   
            }

            // Either the file doesn't exist, or exists but has expired or didn't deserialize correctly,
            // download and save a fresh copy.  First ensure that the timestamp has expired.

            if (Callrate > 0) {

                DateTime now = DateTime.Now;
                if (now < _nextFetchStamp) {
                    TimeSpan delay = _nextFetchStamp - now;                    
                    await Task.Delay((int) delay.TotalMilliseconds);
                }
                
                _nextFetchStamp = now.AddSeconds(1f / Callrate);
            }
            
            // Download file, store in cache and return the body

            Console.WriteLine($"fetching {uri}, ");

            HttpRequestMessage request = new HttpRequestMessage {
                Method = HttpMethod.Get,
                RequestUri = new Uri(uri)
            };
            
            string body;

            try {

                HttpResponseMessage response = await _client.SendAsync(request);

                // A pending manager (or a GW before they started) has no picks yet;
                // the endpoint returns 404. Callers can opt to tolerate that and get
                // a null back rather than exiting. Do NOT cache the 404.
                if (tolerateNotFound && response.StatusCode == HttpStatusCode.NotFound) {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                body = await response.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(filename, body);
                return body;   

            } catch (Exception e) {

                Console.WriteLine($"error: {e.Message}");
				Environment.Exit(-1);
				return null;  // Unreachable but required for compiler
            }
        }
	}
}