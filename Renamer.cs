using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Relocation;

namespace ShokoRenamer
{
    public class Plugin : IPlugin
    {
        public Guid ID => UuidUtility.GetV5(typeof(Plugin).FullName!);
        public string Name => "ShokoRenamer";
        public string? Description => "ShokoRenamer";
    }

    /// <summary>
    /// Produces filenames in the format:
    /// <c>{AnimeName} - {EpisodeNumber} ({Resolution} {Codec}{Source}) ({CRC}) [{ReleaseGroup}].{ext}</c>
    /// </summary>
    public partial class MyRenamer(ILogger<MyRenamer> logger) : IRelocationProvider
    {
        public string Name => "ShokoRenamer";
        public string? Description => "ShokoRenamer";

        public RelocationResult GetPath(RelocationContext ctx)
        {
            var result = new RelocationResult();

            // Early guard: check for series and episodes
            if (ctx.Series.Count == 0)
                return RelocationResult.FromError("No series found for the video file.");
            if (ctx.Episodes.Count == 0)
                return RelocationResult.FromError("No episodes found for the video file.");

            if (ctx.RenameEnabled)
            {
                var fileResult = GetFilename(ctx);
                if (fileResult.Error != null)
                    return fileResult;
                result.FileName = fileResult.FileName;
                // ReplaceInvalidPathCharacters is called inside GetFilename
            }
            else
            {
                result.SkipRename = true;
            }

            if (ctx.MoveEnabled)
            {
                var preferredSeriesTitle = ctx.Series[0].PreferredTitle?.Value ?? ctx.Series[0].Title;

                if (ctx.Groups.Count > 0 && ctx.Groups[0].Series.Count > 1)
                {
                    var preferredGroupTitle = ctx.Groups[0].PreferredTitle?.Value;
                    if (preferredGroupTitle != null)
                    {
                        result.Path = Path.Combine(preferredGroupTitle, preferredSeriesTitle).ReplaceInvalidPathCharacters();
                    }
                    else
                    {
                        result.SkipMove = true;
                    }
                }
                else
                {
                    result.Path = preferredSeriesTitle.ReplaceInvalidPathCharacters();
                }

                var destinationFolder = ctx.AvailableFolders.FirstOrDefault(a => a.DropFolderType.HasFlag(DropFolderType.Destination));
                if (destinationFolder != null)
                {
                    result.ManagedFolder = destinationFolder;
                }
                else
                {
                    result.SkipMove = true;
                }
            }
            else
            {
                result.SkipMove = true;
            }

            return result;
        }

        /// <summary>
        /// Generates the filename using metadata or falls back to regex/filename parsing when <c>MediaInfo</c> is unavailable.
        /// </summary>
        private RelocationResult GetFilename(RelocationContext args)
        {
            var animeInfo = args.Series[0];
            var episodeInfo = args.Episodes;
            var videoInfo = args.Video;
            var fileInfo = args.File;

            var mediaInfo = videoInfo.MediaInfo?.VideoStream;
            var releaseInfo = videoInfo.ReleaseInfo;

            if (releaseInfo == null)
            {
                const string errorMessage = "Release info not found!";
                logger.LogWarning(errorMessage);
                return RelocationResult.FromError(errorMessage);
            }

            var animeName = animeInfo.PreferredTitle?.Value ?? animeInfo.Title;
            var episodeTitleOrNumber = GetEpisodeTitleOrNumber(animeInfo, episodeInfo);

            string resolution;
            if (mediaInfo is not null)
            {
                resolution = $"{mediaInfo.Width}x{mediaInfo.Height}";
            }
            else
            {
                resolution = ResolutionRegex().Match(fileInfo.FileName).Value;
            }

            string codec;
            if (mediaInfo is not null)
            {
                codec = mediaInfo.Codec.Simplified.ToUpperInvariant();
            }
            else
            {
                if (fileInfo.FileName.Contains("AV1", StringComparison.OrdinalIgnoreCase))
                    codec = "AV1";
                else if (fileInfo.FileName.Contains("HEVC", StringComparison.OrdinalIgnoreCase))
                    codec = "HEVC";
                else
                    codec = "H264";
            }

            var source = releaseInfo.Source.ToString();
            var crc = videoInfo.Hashes.FirstOrDefault(hash => hash.Type.Equals("CRC32"))?.Value;
            var releaseGroup = releaseInfo.Group?.ShortName;

            logger.LogInformation("Renaming: Anime={Anime} Episode={Episode} Resolution={Resolution} Codec={Codec} Source={Source} CRC={Crc} Group={Group}",
                animeName, episodeTitleOrNumber, resolution, codec, source, crc, releaseGroup);

            var result = $"{animeName} - {episodeTitleOrNumber} ({resolution} {codec} {source}) ({crc}) [{releaseGroup}]";

            if (fileInfo.FileName.Contains("Fast", StringComparison.OrdinalIgnoreCase) &&
                fileInfo.FileName.Contains("Release", StringComparison.OrdinalIgnoreCase))
                result += " Fast Release";

            result += Path.GetExtension(fileInfo.FileName);
            result = result.ReplaceInvalidPathCharacters();
            return new RelocationResult { FileName = result };
        }

        /// <summary>
        /// For Movies: returns the episode title(s).
        /// For other types: returns episode numbers with type-based prefixes.
        /// Non-Episode types include the title after the number (e.g. "S01 - Special Name").
        /// </summary>
        private static string GetEpisodeTitleOrNumber(IShokoSeries animeInfo, IReadOnlyList<IShokoEpisode> episodes)
        {
            var titles = string.Join(", ", episodes.Select(e => e.PreferredTitle?.Value ?? e.Title));

            if (animeInfo.Type == AnimeType.Movie)
                return titles;

            var numbers = string.Join("-", episodes
                .Where(e => e.Type == episodes[0].Type)
                .Select(e => GetEpisodeNumber(e, animeInfo)));

            return episodes[0].Type == EpisodeType.Episode
                ? numbers
                : $"{numbers} - {titles}";
        }

        /// <summary>
        /// Returns episode numbers with type prefixes (S=Special, C=Credits, T=Trailer, etc.).
        /// Uses zero-padding based on the total count per type, with a minimum of 2 digits for Episodes and Specials.
        /// </summary>
        private static string GetEpisodeNumber(IShokoEpisode episodeInfo, IShokoSeries animeInfo)
        {
            var episodeCount = animeInfo.EpisodeCounts[episodeInfo.Type];
            var prefix = episodeInfo.Type switch
            {
                EpisodeType.Credits => "C",
                EpisodeType.Special => "S",
                EpisodeType.Trailer => "T",
                EpisodeType.Parody => "P",
                EpisodeType.Other => "O",
                _ => ""
            };

            if (episodeInfo.Type is EpisodeType.Episode or EpisodeType.Special)
                return prefix + episodeInfo.EpisodeNumber.PadZeroes(Math.Max(episodeCount, 10));

            return prefix + episodeInfo.EpisodeNumber.PadZeroes(episodeCount);
        }

        /// <summary>
        /// Matches resolution patterns like "1920x1080" in filenames when <c>MediaInfo</c> is unavailable.
        /// </summary>
        [GeneratedRegex(@"\d+x\d+")]
        private static partial Regex ResolutionRegex();
    }
}
