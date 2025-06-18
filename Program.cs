using BeautifulLyricsAndroid.Entities;
using BeautifulLyricsMobileV2.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpotifyAPI.Web;
using System.Text;

namespace BeautifulLyricsToLrc
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string directory = "";

            if(File.Exists("directory.txt") && !string.IsNullOrWhiteSpace(File.ReadAllText("directory.txt")))
                directory = File.ReadAllText("directory.txt").Trim();
            else
            {
                Console.WriteLine("What directory do you want the files saved in? (Paste the directory here)");
                directory = Console.ReadLine();

                File.WriteAllText("directory.txt", directory.Trim());
                Console.WriteLine();
            }

            Directory.CreateDirectory(directory);

            var config = SpotifyClientConfig.CreateDefault();

            var request = new ClientCredentialsRequest("4d42ec7301a64d57bc1971655116a3b9", "0423d7b832114aa086a2034e2cde0138"); // oh no, my secret is in here!
            var authResponse = await new OAuthClient(config).RequestToken(request);

            SpotifyClient spotify = new SpotifyClient(config.WithToken(authResponse.AccessToken));

            while(true)
            {
                Console.Clear();
                await DoThing(spotify, directory);
            }
        }

        static async Task DoThing(SpotifyClient spotify, string directory)
        {
            // https://open.spotify.com/track/59KOoHFcw5XfICnO57holu?si=e1c02f861122456f
            Console.WriteLine("What is the Spotify URL?");
            string spotifyUrl = Console.ReadLine();

            if(spotifyUrl.ToLower() == "exit" || spotifyUrl.ToLower() == "quit" || spotifyUrl.ToLower() == "q")
                Environment.Exit(0);

            if (!Uri.IsWellFormedUriString(spotifyUrl, UriKind.Absolute) || !spotifyUrl.Contains("spotify"))
            {
                Console.WriteLine("Invalid URL");
                return;
            }

            string spotifyId = spotifyUrl.Split('/')[4].Trim().Split('?')[0].Trim();

            FullTrack track = await spotify.Tracks.Get(spotifyId);

            using HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("https://beautiful-lyrics.socalifornian.live/lyrics/")
            };

            client.DefaultRequestHeaders.Add("Authorization", "Bearer BQDLdaKMJYKr8LRep_MvqrQfV72ty65wFQ4oXYuPM9AaPVcEOjqbLh3UAcSzQpOckxn4cfWn9hfDFJ-1W0scDjl214UjytYJYG-fOsqNOYvWbttLWLegqW9o8EoIZecBZbqVSeaa9rUI7qQg4has3p2WD80daDugR2KNU89EVefoFySCVPYSPk9eBKUFgVmOMUCYr8Q7TOj05Jb5Mn2gbKfEkPXOODXjG60pspeOC4jxScu9-Xay4r-ks7bZwKsinu6kvYnUGWbhe-ST2PFmebcDwJxS");

            HttpResponseMessage response = await client.GetAsync(spotifyId);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to retrieve lyrics. {response.StatusCode}");
                Console.ReadLine();

                return;
            }

            string content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                Console.WriteLine("No lyrics found for this song");
                Console.ReadLine();

                return;
            }

            JObject json = JObject.Parse(content);
            string type = json["Type"].ToString();

            List<string> lines = [];

            if (type == "Syllable")
            {
                SyllableSyncedLyrics providerLyrics = JsonConvert.DeserializeObject<SyllableSyncedLyrics>(content);
                TransformedLyrics transformedLyrics = LyricUtilities.TransformLyrics(new ProviderLyrics
                {
                    SyllableLyrics = providerLyrics
                });

                SyllableSyncedLyrics lyrics = transformedLyrics.Lyrics.SyllableLyrics;

                foreach (var line in lyrics.Content)
                {
                    if(line is Interlude interlude)
                    {
                        TimeSpan time = TimeSpan.FromSeconds(interlude.Time.StartTime);
                        string timeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds);

                        lines.Add($"[{timeText}]");
                    }
                    else if (line is SyllableVocalSet set)
                    {
                        StringBuilder sb = new StringBuilder();
                        StringBuilder backgroundSb = new StringBuilder();

                        bool previousIsPartOfWord = false;
                        foreach (var syllable in set.Lead.Syllables)
                        {
                            TimeSpan wordTime = TimeSpan.FromSeconds(syllable.StartTime);
                            string wordTimeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)wordTime.TotalMinutes, wordTime.Seconds, wordTime.Milliseconds);
                            sb.Append(syllable.IsPartOfWord ? $"<{wordTimeText}>{syllable.Text}" : $"<{wordTimeText}>{syllable.Text} ");
                            //sb.Append(syllable.IsPartOfWord ? (previousIsPartOfWord ? syllable.Text : $"<{wordTimeText}>{syllable.Text}") : (previousIsPartOfWord ? $"{syllable.Text} " : $"<{wordTimeText}>{syllable.Text} "));
                            previousIsPartOfWord = syllable.IsPartOfWord;
                        }

                        if (set.Background != null)
                        {
                            foreach (var background in set.Background)
                            {
                                foreach (var syllable in background.Syllables)
                                {
                                    TimeSpan wordTime = TimeSpan.FromSeconds(syllable.StartTime);
                                    string wordTimeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)wordTime.TotalMinutes, wordTime.Seconds, wordTime.Milliseconds);

                                    backgroundSb.Append(syllable.IsPartOfWord ? $"<{wordTimeText}>{syllable.Text}" : $"<{wordTimeText}>{syllable.Text} ");
                                }
                            }
                        }

                        string lineContent = sb.ToString().Trim();
                        TimeSpan time = TimeSpan.FromSeconds(set.Lead.StartTime);
                        string timeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds);
                        TimeSpan endTime = TimeSpan.FromSeconds(set.Lead.EndTime);
                        string endTimeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)endTime.TotalMinutes, endTime.Seconds, endTime.Milliseconds);

                        string alignment = set.OppositeAligned ? "v2:" : "v1:";
                        lines.Add($"[{timeText}]{alignment} {lineContent}<{endTimeText}>");

                        if(set.Background != null)
                        {
                            TimeSpan backgroundTime = TimeSpan.FromSeconds(set.Background[0].StartTime);
                            string backgroundTimeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)backgroundTime.TotalMinutes, backgroundTime.Seconds, backgroundTime.Milliseconds);
                            TimeSpan backgroundEndTime = TimeSpan.FromSeconds(set.Background[0].EndTime);
                            string backgroundEndTimeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)backgroundEndTime.TotalMinutes, backgroundEndTime.Seconds, backgroundEndTime.Milliseconds);

                            lines.Add($"[{backgroundTimeText}][bg:]{alignment} {backgroundSb.ToString().Trim()}<{backgroundEndTimeText}>");
                        }
                    }
                }
            }
            else if (type == "Line")
            {
                LineSyncedLyrics providerLyrics = JsonConvert.DeserializeObject<LineSyncedLyrics>(content);
                TransformedLyrics transformedLyrics = LyricUtilities.TransformLyrics(new ProviderLyrics
                {
                    LineLyrics = providerLyrics
                });

                LineSyncedLyrics lyrics = transformedLyrics.Lyrics.LineLyrics;

                foreach (var line in lyrics.Content)
                {
                    if (line is Interlude interlude)
                    {
                        TimeSpan time = TimeSpan.FromSeconds(interlude.Time.StartTime);
                        string timeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds);

                        lines.Add($"[{timeText}]");
                    }
                    else if (line is LineVocal vocal)
                    {
                        TimeSpan time = TimeSpan.FromSeconds(vocal.StartTime);
                        string timeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds);

                        lines.Add($"[{timeText}] {vocal.Text}");
                    }
                }
            }
            else
            {
                Console.WriteLine("Static lyrics, no time data available");
                Console.ReadLine();

                return;
            }

            File.WriteAllText(Path.Combine(directory, $"{track.Artists[0].Name} - {track.Name}.lrc"), string.Join("\n", lines));
            Console.WriteLine($"Done! Lyrics saved to {Path.Combine(directory, $"{spotifyId}.lrc")}");

            Console.ReadLine();
        }
    }
}
