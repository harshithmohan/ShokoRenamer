using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Relocation;

namespace Shoko.Plugin.Renamer
{
    public class Plugin : IPlugin
    {
        public Guid ID => UuidUtility.GetV5(typeof(Plugin).FullName!);

        public string Name => nameof(MyRenamer);

        public string Description => "My custom renamer";
    }

    public partial class MyRenamer (ILogger<MyRenamer> logger) : IRelocationProvider
    {
        public string Name => "CustomRenamer";

        public RelocationResult GetPath(RelocationContext ctx)
        {
            var result = new RelocationResult();

            if (ctx.RenameEnabled)
            {
                string filename;
                try
                {
                    filename = GetFilename(ctx);
                }
                catch (RenamerException e)
                {
                    result.Error = new RelocationError(e.Message);
                    return result;
                }

                result.FileName = filename.ReplaceInvalidPathCharacters();
            }
            else
            {
                result.SkipRename = true;
            }

            if (ctx.MoveEnabled)
            {
                var preferredSeriesTitle = ctx.Series[0].PreferredTitle?.Value;

                if (ctx.Groups[0].Series.Count > 1)
                {
                    var preferredGroupTitle = ctx.Groups[0].PreferredTitle?.Value;
                    if (preferredGroupTitle != null && preferredSeriesTitle != null)
                    {
                        result.Path = Path.Combine(preferredGroupTitle, preferredSeriesTitle).ReplaceInvalidPathCharacters();
                    }
                }
                else
                {
                    if (preferredSeriesTitle != null)
                        result.Path = preferredSeriesTitle.ReplaceInvalidPathCharacters();
                }

                result.ManagedFolder = ctx.AvailableFolders.First(a => a.DropFolderType.HasFlag(DropFolderType.Destination));
            }
            else
            {
                result.SkipMove = true;
            }

            return result;
        }

        private string GetFilename(RelocationContext args)
        {
            var animeInfo = args.Series[0];
            var episodeInfo = args.Episodes.ToList();
            var videoInfo = args.Video;
            var fileInfo = args.File;

            var mediaInfo = videoInfo.MediaInfo?.VideoStream;
            var releaseInfo = videoInfo.ReleaseInfo;

            if (releaseInfo == null)
            {
                const string errorMessage = "Release info not found!";
                logger.LogInformation(errorMessage);
                throw new RenamerException(errorMessage);
            }

            // Get the preferred title (aka Overriden title)
            var animeName = animeInfo.PreferredTitle;
            logger.LogInformation("Anime Name: {AnimeName}", animeName);

            var episodeTitleOrNumber = GetEpisodeTitleOrNumber(animeInfo, episodeInfo);
            logger.LogInformation("Episode Number or Title: {EpisodeTitleOrNumber}", episodeTitleOrNumber);

            string resolution;
            try
            {
                resolution = $"{mediaInfo!.Width}x{mediaInfo.Height}";
            }
            catch (Exception)
            {
                resolution = MyRegex().Match(fileInfo.FileName).Value;
            }
            logger.LogInformation("Resolution: {Resolution}", resolution);

            string codec;
            try
            {
                codec = mediaInfo!.Codec.Simplified.ToUpper();
            }
            catch (Exception)
            {
                if (fileInfo.FileName.Contains("AV1", StringComparison.InvariantCultureIgnoreCase))
                {
                    codec = "AV1";
                }
                else if (fileInfo.FileName.Contains("HEVC", StringComparison.InvariantCultureIgnoreCase))
                {
                    codec = "HEVC";
                }
                else
                {
                    codec = "H264";
                }
            }
            logger.LogInformation("Codec: {Codec}", codec);

            var source = releaseInfo.Source.ToString();
            logger.LogInformation("Source: {Source}", source);

            var crc = videoInfo.Hashes.FirstOrDefault(hash => hash.Type.Equals("CRC32"))?.Value;
            logger.LogInformation("CRC: {Crc}", crc);

            var releaseGroup = releaseInfo.Group?.ShortName;
            logger.LogInformation("Release Group: {ReleaseGroup}", releaseGroup);

            // build a string like "Tokyo Revengers - 24 (1920x1080 HEVC BD) (95624E85) [Hi10].mkv"
            var result = $"{animeName} - {episodeTitleOrNumber} ({resolution} {codec}{source}) ({crc}) [{releaseGroup}]";

            if (fileInfo.FileName.Contains("Fast", StringComparison.InvariantCultureIgnoreCase) && fileInfo.FileName.Contains("Release", StringComparison.InvariantCultureIgnoreCase))
            {
                result += " Fast Release";
            }

            result += Path.GetExtension(fileInfo.FileName);

            // Remove invalid characters
            result = result.ReplaceInvalidPathCharacters();

            return result;
        }

        private static string GetEpisodeTitleOrNumber(IShokoSeries animeInfo, List<IShokoEpisode> episodesInfo)
        {
            var episodeTitleOrNumber = "";

            var allEpisodesTitle = episodesInfo.Select(info => info.PreferredTitle).ToList();

            if (animeInfo.Type == AnimeType.Movie || episodesInfo[0].Type != EpisodeType.Episode)
            {
                episodeTitleOrNumber = string.Join(", ", allEpisodesTitle);
            }

            if (animeInfo.Type == AnimeType.Movie)
            {
                return episodeTitleOrNumber;
            }

            var episodeNumbers =
                episodesInfo
                    .Where(info => info.Type == episodesInfo[0].Type)
                    .Select(info => GetEpisodeNumber(info, animeInfo))
                    .ToList();

            var paddedEpisodeNumber = string.Join("-", episodeNumbers);

            episodeTitleOrNumber = episodesInfo[0].Type != EpisodeType.Episode
                ? $"{paddedEpisodeNumber} - {episodeTitleOrNumber}"
                : paddedEpisodeNumber;

            return episodeTitleOrNumber;
        }

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
            {
                return prefix + episodeInfo.EpisodeNumber.PadZeroes(Math.Max(episodeCount, 10));
            }

            return prefix + episodeInfo.EpisodeNumber.PadZeroes(episodeCount);
        }

        [GeneratedRegex(@"\d+x\d+")]
        private static partial Regex MyRegex();
    }
}
