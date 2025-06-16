using BeautifulLyricsAndroid.Entities;
using Newtonsoft.Json;
using System.Linq;

namespace BeautifulLyricsMobileV2.Entities
{
    internal static class LyricUtilities
    {
        internal static TransformedLyrics TransformLyrics(ProviderLyrics providedLyrics)
        {
            TransformedLyrics lyrics = new TransformedLyrics
            {
                Lyrics = providedLyrics
            };

            List<TimeMetadata> vocalTimes = [];

            if (lyrics.Lyrics.LineLyrics is LineSyncedLyrics lineLyrics)
            {
                try
                {
                    List<LineVocal> lineVocals = [];

                    foreach (var vocalGroup in lineLyrics.Content)
                    {
                        LineVocal deserialize = JsonConvert.DeserializeObject<LineVocal>(vocalGroup.ToString());

                        if (deserialize is LineVocal vocal)
                        {
                            lineVocals.Add(vocal);

                            vocalTimes.Add(new TimeMetadata
                            {
                                StartTime = vocal.StartTime,
                                EndTime = vocal.EndTime
                            });
                        }
                    }

                    lineLyrics.Content = [.. lineVocals];

                    bool addedStartInterlude = false;
                    for (int i = vocalTimes.Count - 1; i > (addedStartInterlude ? 1 : 0); i--)
                    {
                        var endingVocalGroup = vocalTimes[i];
                        var startingVocalGroup = vocalTimes[i - 1];

                        if (endingVocalGroup.StartTime - startingVocalGroup.EndTime >= 2)
                        {
                            TimeMetadata newTime = new TimeMetadata
                            {
                                StartTime = startingVocalGroup.EndTime,
                                EndTime = endingVocalGroup.StartTime - 0.25d
                            };

                            lineLyrics.Content.Insert(i, new Interlude
                            {
                                Time = newTime
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            else if (lyrics.Lyrics.SyllableLyrics is SyllableSyncedLyrics syllableLyrics)
            {
                List<string> lines = [];
                List<SyllableVocalSet> vocalLines = [];

                foreach (var vocalGroup in syllableLyrics.Content)
                {
                    SyllableVocalSet syllableVocalSet = JsonConvert.DeserializeObject<SyllableVocalSet>(vocalGroup.ToString());

                    if (syllableVocalSet is SyllableVocalSet vocalSet)
                    {
                        vocalLines.Add(syllableVocalSet);

                        double startTime = vocalSet.Lead.StartTime;
                        double endTime = vocalSet.Lead.EndTime;

                        vocalTimes.Add(new TimeMetadata
                        {
                            StartTime = startTime,
                            EndTime = endTime
                        });
                    }
                }

                syllableLyrics.Content = [.. vocalLines];

                bool addedStartInterlude = false;

                for (int i = vocalTimes.Count - 1; i > (addedStartInterlude ? 1 : 0); i--)
                {
                    var endingVocalGroup = vocalTimes[i];
                    var startingVocalGroup = vocalTimes[i - 1];

                    if (endingVocalGroup.StartTime - startingVocalGroup.EndTime >= 2)
                    {
                        TimeMetadata newTime = new TimeMetadata
                        {
                            StartTime = startingVocalGroup.EndTime,
                            EndTime = endingVocalGroup.StartTime - 0.25f
                        };

                        syllableLyrics.Content.Insert(i, new Interlude
                        {
                            Time = newTime
                        });
                    }
                }

            }

            return lyrics;
        }
    }

    internal class TransformedLyrics
    {
        public NaturalAlignment NaturalAlignment { get; set; }
        public string Language { get; set; }
        public string? RomanizedLanguage { get; set; }

        public ProviderLyrics Lyrics { get; set; }
    }

    internal enum NaturalAlignment
    {
        Right,
        Left
    }
}