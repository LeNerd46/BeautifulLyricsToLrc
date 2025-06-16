using BeautifulLyricsAndroid.Entities;
using BeautifulLyricsMobileV2.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace BeautifulLyricsToLrc
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("What directory do you want the files saved in? (Paste the directory here)");
            string directory = Console.ReadLine();

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            Console.WriteLine();
            Console.WriteLine("What is the Spotify ID?");
            string spotifyId = Console.ReadLine();

            HttpClient client = new HttpClient
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

                        bool previousIsPartOfWord = false;
                        foreach (var syllable in set.Lead.Syllables)
                        {
                            TimeSpan wordTime = TimeSpan.FromSeconds(syllable.StartTime);
                            string wordTimeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)wordTime.TotalMinutes, wordTime.Seconds, wordTime.Milliseconds);
                            sb.Append(syllable.IsPartOfWord ? (previousIsPartOfWord ? syllable.Text : $"<{wordTimeText}>{syllable.Text}") : (previousIsPartOfWord ? $"{syllable.Text} " : $"<{wordTimeText}>{syllable.Text} "));
                            previousIsPartOfWord = syllable.IsPartOfWord;
                        }

                        // For line by line use
                        /*if (set.Background != null)
                        {
                            int i = 0;

                            foreach (var background in set.Background)
                            {
                                foreach (var syllable in background.Syllables)
                                {
                                    if (i == 0)
                                        sb.Append(syllable.IsPartOfWord ? $"({syllable.Text}" : $"({syllable.Text} ");
                                    else if (i == set.Background[0].Syllables.Count - 1)
                                        sb.Append(syllable.IsPartOfWord ? $"{syllable.Text})" : $"{syllable.Text})");
                                    else
                                        sb.Append(syllable.IsPartOfWord ? $"{syllable.Text}" : $"{syllable.Text} ");

                                    i++;
                                }
                            }
                        }*/

                        string lineContent = sb.ToString().Trim();
                        TimeSpan time = TimeSpan.FromSeconds(set.Lead.StartTime);
                        string timeText = string.Format("{0:D2}:{1:D2}.{2:D2}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds);

                        lines.Add($"[{timeText}] {lineContent}");
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

            File.WriteAllText(Path.Combine(directory, $"{spotifyId}.lrc"), string.Join("\n", lines));
            Console.WriteLine($"Done! Lyrics saved to {Path.Combine(directory, $"{spotifyId}.lrc")}");

            Console.ReadLine();
        }
    }
}
