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
    public class Renamer : IRenamer
    {
        private readonly ILogger<Renamer> _logger;

        public Renamer(ILogger<Renamer> logger)
        {
            _logger = logger;
        }

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

            result.FileName = filename;
            result.Path = args.Series[0].PreferredTitle.ReplaceInvalidPathCharacters();
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
                var errorMessage = "Video info not found!";
                _logger.LogInformation(errorMessage);
                return $"RENAMER_ERROR: {errorMessage}";
            }

            var mediaInfo = videoInfo.MediaInfo?.Video;
            var anidbFileInfo = videoInfo.AniDB;

            if (anidbFileInfo == null)
            {
                var errorMessage = "AniDB info not found!";
                _logger.LogInformation(errorMessage);
                return $"RENAMER_ERROR: {errorMessage}";
            }

            // Get the preferred title (aka Overriden title)
            var animeName = animeInfo.PreferredTitle;
            _logger.LogInformation($"Anime Name: {animeName}");

            var episodeTitleOrNumber = GetEpisodeTitleOrNumber(animeInfo, episodeInfo);
            _logger.LogInformation($"Episode Number or Title: {episodeTitleOrNumber}");

            var resolution = "";
            try
            {
                resolution = $"{mediaInfo!.Width}x{mediaInfo!.Height}";
            }
            catch (Exception)
            {
                resolution = Regex.Match(fileInfo.FileName, @"\d+x\d+").Value;
            }
            _logger.LogInformation($"Resolution: {resolution}");

            var codec = "";
            try
            {
                codec = mediaInfo!.SimplifiedCodec;
            }
            catch (Exception)
            {
                codec = fileInfo.FileName.Contains("HEVC") ? "HEVC" : "H264";
            }
            _logger.LogInformation($"Codec: {codec}");

            var source = anidbFileInfo.Source;
            _logger.LogInformation($"Source: {source}");

            if (source.Contains("TV")) source = " TV";
            else if (source.Contains("DVD")) source = " DVD";
            else
                source = source switch
                {
                    "BluRay" => " BD",
                    "Web" => " Web",
                    _ => ""
                };
            _logger.LogInformation($"Simplified source: {source}");

            var crc = videoInfo.Hashes!.CRC;
            _logger.LogInformation($"CRC: {crc}");

            var releaseGroup = anidbFileInfo.ReleaseGroup.ShortName;
            _logger.LogInformation($"Release Group: {releaseGroup}");

            // build a string like "Tokyo Revengers - 24 (1920x1080 HEVC BD) (95624E85) [Hi10].mkv"
            var result = $"{animeName} - {episodeTitleOrNumber} ({resolution} {codec}{source}) ({crc}) [{releaseGroup}]";

            if (fileInfo.FileName.Contains("Fast") && fileInfo.FileName.Contains("Release"))
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
            string episodeTitleOrNumber = "";

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
    }
}
