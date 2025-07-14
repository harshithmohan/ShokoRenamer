using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Renamer
{
    [RenamerID("CustomRenamer")]
    public partial class Renamer (ILogger<Renamer> logger) : IRenamer
    {
        public string Name => "CustomRenamer";

        public string Description => "My custom renamer";

        public bool SupportsMoving => true;

        public bool SupportsRenaming => true;

        public RelocationResult GetNewPath(RelocationEventArgs args)
        {
            var result = new RelocationResult();

            var filename = GetFilename(args);

            if (filename.StartsWith("RENAMER_ERROR"))
            {
                args.Cancel = true;
                result.Error = new RelocationError(filename.Replace("RENAMER_ERROR: ", ""));
                return result;
            }

            if (args.Groups[0].Series.Count > 1)
            {
                result.Path = Path.Combine(
                    args.Groups[0].PreferredTitle.ReplaceInvalidPathCharacters(),
                    args.Series[0].PreferredTitle.ReplaceInvalidPathCharacters()
                );
            }
            else
            {
                result.Path = args.Series[0].PreferredTitle.ReplaceInvalidPathCharacters();
            }

            result.FileName = filename;
            result.DestinationImportFolder = args.AvailableFolders.First(a => a.DropFolderType.HasFlag(DropFolderType.Destination));

            return result;
        }

        private string GetFilename(RelocationEventArgs args)
        {
            var animeInfo = args.Series[0];
            var episodeInfo = args.Episodes.ToList();
            var videoInfo = args.File.Video;
            var fileInfo = args.File;

            if (videoInfo == null)
            {
                const string errorMessage = "Video info not found!";
                logger.LogInformation(errorMessage);
                return $"RENAMER_ERROR: {errorMessage}";
            }

            var mediaInfo = videoInfo.MediaInfo?.VideoStream;
            var anidbFileInfo = videoInfo.AniDB;

            if (anidbFileInfo == null)
            {
                const string errorMessage = "AniDB info not found!";
                logger.LogInformation(errorMessage);
                return $"RENAMER_ERROR: {errorMessage}";
            }

            // Get the preferred title (aka Overriden title)
            var animeName = animeInfo.PreferredTitle;
            logger.LogInformation("Anime Name: {AnimeName}", animeName);

            var episodeTitleOrNumber = GetEpisodeTitleOrNumber(animeInfo, episodeInfo);
            logger.LogInformation("Episode Number or Title: {EpisodeTitleOrNumber}", episodeTitleOrNumber);

            string resolution;
            try
            {
                resolution = $"{mediaInfo!.Width}x{mediaInfo!.Height}";
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

            var source = anidbFileInfo.Source;
            logger.LogInformation("Source: {Source}", source);

            if (source.Contains("TV", StringComparison.InvariantCultureIgnoreCase)) source = " TV";
            else if (source.Contains("DVD", StringComparison.InvariantCultureIgnoreCase)) source = " DVD";
            else
                source = source switch
                {
                    "BluRay" => " BD",
                    "Web" => " Web",
                    _ => ""
                };
            logger.LogInformation("Simplified source: {Source}", source);

            var crc = videoInfo.Hashes!.CRC;
            logger.LogInformation("CRC: {Crc}", crc);

            var releaseGroup = anidbFileInfo.ReleaseGroup.ShortName;
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
