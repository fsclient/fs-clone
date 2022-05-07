namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;

    using Newtonsoft.Json.Linq;

    public static class ProviderHelper
    {
        public static bool ShouldUkrainianProvidersBeEnabledByDefault => RegionInfo.CurrentRegion.TwoLetterISORegionName.Equals("uk", StringComparison.OrdinalIgnoreCase)
            || CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("uk", StringComparison.OrdinalIgnoreCase)
            || CultureInfo.CurrentCulture.TwoLetterISOLanguageName.Equals("uk", StringComparison.OrdinalIgnoreCase);

        public static IEnumerable<string> GetTitles(this ItemInfo item, bool preferOnlyOrigin = false, bool includeTrimmed = true)
        {
            var ru = item.Details.Titles ?? Array.Empty<string>();
            var org = (item.Details.TitleOrigin ?? "")
                .Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries);
            var titles = preferOnlyOrigin && org.Length > 0 ? org : org.Concat(ru);

            if (!includeTrimmed)
            {
                return titles.Take(5);
            }

            var trimmed = titles
                .Select(m => m
                    .Split(new[] { ". " }, StringSplitOptions.None)
                    .First());

            return titles.Union(trimmed).Take(5);
        }

        public static IEnumerable<Video> ParseVideosFromM3U8(string[] lines, Uri? mainLink = null)
        {
            var leftPart = string.Empty;
            if (mainLink != null)
            {
                var mainUriStr = mainLink.ToString();
                leftPart = mainUriStr[..mainUriStr.LastIndexOf('/')] + "/";
            }

            for (var i = 0; i < lines.Length; i++)
            // #EXT-X-STREAM-INF:RESOLUTION=640x360,BANDWIDTH=378000
            // link
            {
                if (lines[i].StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal))
                {
                    if (i + 1 == lines.Length)
                    {
                        break;
                    }

                    var relativeOrAbsoluteUriStr = lines[i + 1];
                    if (!Uri.TryCreate(relativeOrAbsoluteUriStr, UriKind.Absolute, out var link)
                        && !Uri.TryCreate(leftPart + relativeOrAbsoluteUriStr, UriKind.Absolute, out link))
                    {
                        continue;
                    }

                    var video = new Video(link);

                    var indexOfX = lines[i].IndexOf('x') + 1;
                    if (indexOfX > 0)
                    {
                        var indexOfComma = lines[i].IndexOf(',', indexOfX);

                        video.Quality = indexOfComma > 0
                            ? lines[i][indexOfX..indexOfComma]
                            : lines[i][indexOfX..];
                    }

                    yield return video;
                    i++;
                }
            }
        }

        /// <summary>
        /// Parses key-value pairs from PlayerJS pattern
        /// </summary>
        /// <param name="input">String like "[360p]https://link/360 or https://link/360_2, [720p]https://link/720/"</param>
        /// <returns>Enumerable of videos</returns>
        public static IEnumerable<(string key, string? subKey, string value)> ParsePlayerJsKeyValuePairs(string input)
        {
            var currentQuality = string.Empty;
            var currentSubKey = (string?)null;
            int qualityStart = -1, subKeyStart = -1, videoVariantStart = -1;
            for (var i = 0; i < input!.Length; i++)
            {
                var current = input[i];

                if (char.IsWhiteSpace(current))
                {
                    continue;
                }

                // parse "[quality]"
                else if (current == '[')
                {
                    qualityStart = i + 1;
                }
                else if (current == ']'
                    && qualityStart >= 0)
                {
                    currentQuality = input[qualityStart..i];
                    videoVariantStart = i + 1;
                    qualityStart = -1;
                }

                // parse "{subkey}"
                else if (current == '{')
                {
                    subKeyStart = i + 1;
                }
                else if (current == '}'
                    && subKeyStart >= 0)
                {
                    currentSubKey = input[subKeyStart..i];
                    videoVariantStart = i + 1;
                    subKeyStart = -1;
                }

                // parse "videovariant1 or videovariant2"
                else if (
                    ((i >= 1 && current == 'r' && input[i - 1] == 'o' && input[i - 2] == ' ')
                    || (i >= 2 && current == 'd' && input[i - 1] == 'n' && input[i - 2] == 'a' && input[i - 3] == ' '))
                    && videoVariantStart >= 0)
                {
                    yield return (currentQuality, currentSubKey, input[videoVariantStart..(i - 2)]);
                    videoVariantStart = i + 2;
                }
                else if (videoVariantStart >= 0
                    && (current == ','
                    || current == ';'))
                {
                    var linkStr = input[videoVariantStart..i];
                    yield return (currentQuality, currentSubKey, linkStr);
                    videoVariantStart = -1;
                }
                else if (videoVariantStart >= 0
                    && i == input.Length - 1)
                {
                    var linkStr = input[videoVariantStart..(i + 1)];
                    yield return (currentQuality, currentSubKey, linkStr);
                    videoVariantStart = -1;
                }
                else if (qualityStart < 0 && subKeyStart < 0 && videoVariantStart < 0)
                {
                    videoVariantStart = i;
                }
            }
        }

        /// <summary>
        /// Parses videos from PlayerJS pattern
        /// </summary>
        /// <param name="videosStr">String like "[360p]https://link/360 or https://link/360_2, [720p]https://link/720/"</param>
        /// <param name="domain">Base domain for video links</param>
        /// <returns>Enumerable of tuple of audio and videos</returns>
        public static IEnumerable<(string? audio, Video video)> ParseVideosFromPlayerJsString(string videosStr, Uri domain)
        {
            return ParsePlayerJsKeyValuePairs(videosStr)
                .DistinctBy(tuple => tuple.value)
                .Select(tuple => (
                    quality: tuple.key,
                    audio: tuple.subKey,
                    link: tuple.value.ToUriOrNull(domain)
                ))
                .Where(tuple => tuple.link != null)
                .GroupBy(tuple => (tuple.quality, tuple.audio))
                .Select(g => (
                    audio: g.Key.audio,
                    video: new Video(g.OrderBy(l => l.link!.AbsoluteUri.Contains(".m3u8")).Select(l => new VideoVariant(l.link!)))
                    {
                        Quality = g.Key.quality
                    }
                ))!;
        }

        public static IEnumerable<ITreeNode> ParsePlaylistFromPlayerJsString(HttpClient httpClient,
            Site site, string rootId, string playlistOrFileStr, string? translator, string? itemTitle, Uri frameLink, string? defaultSubLanguage)
        {
            if (JsonHelper.ParseOrNull<JArray>(playlistOrFileStr) is JArray jArray)
            {
                // Simplify parsing by adding dummy season, if it isn't exists.
                if (jArray.FirstOrDefault() is JObject firstElement1
                    && firstElement1["file"] != null)
                {
                    var dummySeason = new JObject();
                    dummySeason["playlist"] = jArray;
                    jArray = new JArray(new[] { dummySeason });
                }
                // Simplify parsing by adding dummy translation, if it isn't exists.
                if (jArray.FirstOrDefault() is JObject firstElement
                    && firstElement["playlist"] is JArray)
                {
                    var dummyTranslation = new JObject();
                    dummyTranslation["folder"] = jArray;
                    jArray = new JArray(new[] { dummyTranslation });
                }

                return jArray
                    .OfType<JObject>()
                    .SelectMany(translator =>
                    {
                        var translatorTitle = (translator["title"] ?? translator["comment"])?.ToString();

                        return (translator["folder"] as JArray ?? new JArray())
                            .OfType<JObject>()
                            .SelectMany((season, index) =>
                            {
                                var seasonTitle = (season["title"] ?? season["comment"])?.ToString();
                                var seasonNumber = seasonTitle?.GetDigits().ToIntOrNull() ?? (index + 1);
                                var seasonId = $"{rootId}_{seasonNumber}";
                                if (seasonTitle == null)
                                {
                                    seasonTitle = $"Сезон " + seasonNumber;
                                }

                                var episodes = (season["playlist"] as JArray ?? season["folder"] as JArray ?? new JArray())
                                    .Select(episodeJson =>(
                                        episodeJson,
                                        file: episodeJson["file"]?.ToString(),
                                        id: episodeJson["id"]?.ToIntOrNull()
                                    ))
                                    .Where(tuple => tuple.file != null && tuple.id.HasValue)
                                    .SelectMany(tuple =>
                                    {
                                        if (!tuple.file!.Contains("[") && tuple.file.Contains("m3u8") && tuple.file.ToUriOrNull(frameLink) is Uri singleLink)
                                        {
                                            Func<File, CancellationToken, Task<IEnumerable<Video>>> preloadLambda = (f, ct) => ParseFromM3u8Async(f, singleLink, ct);

                                            var resultTuple = (
                                                audio: (string?)null,
                                                episodeJson: tuple.episodeJson,
                                                id: tuple.id,
                                                videos: Array.Empty<Video>(),
                                                preloadLambda: preloadLambda
                                            );

                                            return new[] { resultTuple }.AsEnumerable()!;
                                        }
                                        else
                                        {
                                            return ParseVideosFromPlayerJsString(tuple.file, frameLink)
                                                .GroupBy(t => t.audio)
                                                .Select(group => (
                                                    audio: group.Key,
                                                    episodeJson: tuple.episodeJson,
                                                    id: tuple.id,
                                                    videos: group.Select(v => v.video).ToArray(),
                                                    preloadLambda: (Func<File, CancellationToken, Task<IEnumerable<Video>>>?)null
                                                ))!;
                                        }
                                    })
                                    .Where(tuple => tuple.preloadLambda != null || tuple.videos.Length > 0)
                                    .Select(tuple =>
                                    {
                                        var episodeTitle = (tuple.episodeJson["title"] ?? tuple.episodeJson["comment"])?.ToString();
                                        var episodeNumber = episodeTitle?.GetDigits().ToIntOrNull();
                                        var fileLink = tuple.episodeJson["file"]?.ToString();
                                        var poster = tuple.episodeJson["poster"]?.ToUriOrNull();
                                        var id = tuple.episodeJson["id"]?.ToIntOrNull();

                                        var fileId = $"{seasonId}_{id}";

                                        if (tuple.audio != null)
                                        {
                                            fileId += "_" + tuple.audio.GetDeterministicHashCode();
                                        }

                                        var file = new File(site, fileId)
                                        {
                                            ItemTitle = itemTitle,
                                            FrameLink = frameLink,
                                            Episode = episodeNumber.ToRange(),
                                            Season = seasonNumber,
                                            PlaceholderImage = poster
                                        };

                                        file.SubtitleTracks.AddRange(ParsePlayerJsKeyValuePairs(tuple.episodeJson["subtitle"]?.ToString() ?? string.Empty)
                                            .Select(tuple => (
                                                title: tuple.key,
                                                success: Uri.TryCreate(tuple.value, UriKind.Absolute, out var subLink),
                                                subLink))
                                            .Where(tuple => tuple.success)
                                            .Select(tuple => new SubtitleTrack(
                                                LocalizationHelper.DetectLanguageNames(tuple.title).FirstOrDefault() ?? defaultSubLanguage,
                                                tuple.subLink)
                                            {
                                                Title = tuple.title
                                            })
                                            .ToList());

                                        if (!episodeNumber.HasValue)
                                        {
                                            file.Title = episodeTitle;
                                        }

                                        if (tuple.preloadLambda != null)
                                        {
                                            file.SetVideosFactory(tuple.preloadLambda);
                                        }
                                        else
                                        {
                                            file.SetVideos(tuple.videos);
                                        }

                                        return (tuple.audio, file);
                                    })
                                    .Where(tuple => tuple.file != null);

                                return episodes
                                    .GroupBy(epTuple => epTuple.audio)
                                    .Select(group => (
                                        translatorTitle: group.Key ?? translatorTitle,
                                        translateId: $"{rootId}_tran_{(group.Key ?? translatorTitle)?.GetDeterministicHashCode()}",
                                        seasonTitle: seasonTitle,
                                        seasonNumber: seasonNumber,
                                        seasonId: seasonId,
                                        episodes: group.Select(t => t.file)
                                    ));
                            });
                    })
                    .GroupBy(t => (t.seasonTitle, t.seasonNumber, t.seasonId))
                    .Select(seasonGroup =>
                    {
                        var seasonFolder = new Folder(site, seasonGroup.Key.seasonId, FolderType.Season, PositionBehavior.Max)
                        {
                            Title = seasonGroup.Key.seasonTitle,
                            Season = seasonGroup.Key.seasonNumber
                        };

                        var translates = seasonGroup.GroupBy(t => (t.translatorTitle, t.translateId))
                            .Select(translateGroup =>
                            {
                                var tranId = $"{translateGroup.Key.translateId}_{seasonGroup.Key.seasonNumber}";
                                var translateFolder = new Folder(site, tranId, FolderType.Translate, PositionBehavior.Average)
                                {
                                    Title = translateGroup.Key.translatorTitle,
                                    Season = seasonGroup.Key.seasonNumber
                                };

                                translateFolder.AddRange(translateGroup.SelectMany(g => g.episodes));

                                return translateFolder;
                            })
                            .ToArray();

                        if (translates.Length > 1)
                        {
                            seasonFolder.AddRange(translates);
                        }
                        else if (translates.Length > 0)
                        {
                            seasonFolder.AddRange(translates[0].ItemsSource);
                        }

                        return seasonFolder;
                    });
            }
            else if (!playlistOrFileStr.Contains("[")
                && playlistOrFileStr.Contains("m3u8")
                && playlistOrFileStr.ToUriOrNull(frameLink) is Uri singleFileLink)
            {
                var file = new File(site, rootId)
                {
                    Title = translator ?? itemTitle,
                    ItemTitle = itemTitle,
                    FrameLink = frameLink
                };
                file.SetVideosFactory((f, ct) => ParseFromM3u8Async(f, singleFileLink, ct));
                return new[] { file };
            }
            else
            {
                var videos = ParseVideosFromPlayerJsString(playlistOrFileStr, frameLink).ToArray();
                if (videos.Length == 0)
                {
                    return Enumerable.Empty<ITreeNode>();
                }

                return videos
                    .GroupBy(tuple => tuple.audio)
                    .Select(group =>
                    {
                        var fileId = group.Key != null
                            ? $"{rootId}_tran_{group.Key.GetDeterministicHashCode()}"
                            : rootId;
                        var file = new File(site, fileId)
                        {
                            Title = group.Key ?? translator ?? itemTitle,
                            ItemTitle = itemTitle,
                            FrameLink = frameLink
                        };
                        file.SetVideos(group.Select(t => t.video).ToArray());
                        return file;
                    });
            }

            async Task<IEnumerable<Video>> ParseFromM3u8Async(File file, Uri playlistLink, CancellationToken cancellationToken)
            {
                var fileText = await httpClient
                    .GetBuilder(playlistLink)
                    .WithHeader("Origin", file.FrameLink!.GetOrigin())
                    .WithHeader("Referer", file.FrameLink!.ToString())
                    .SendAsync(cancellationToken)
                    .AsText()
                    .ConfigureAwait(false);
                if (fileText == null)
                {
                    return Array.Empty<Video>();
                }

                return ParseVideosFromM3U8(fileText.Split('\n'), playlistLink);
            }
        }
    }
}
