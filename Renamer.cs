using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Renamer
{
    public class Renamer : IRenamer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
                result.Error = new MoveRenameError(filename.Replace("RENAMER_ERROR: ", ""));
                return result;
            }

            result.FileName = filename;
            result.Path = args.AnimeInfo[0].PreferredTitle.ReplaceInvalidPathCharacters();
            result.DestinationImportFolder = args.AvailableFolders.First(a => a.DropFolderType.HasFlag(DropFolderType.Destination));

            return result;
        }

        private static string GetFilename(RelocationEventArgs args)
        {
            var animeInfo = args.AnimeInfo[0];
            var episodeInfo = args.EpisodeInfo.ToList();
            var videoInfo = args.FileInfo.VideoInfo;
            var fileInfo = args.FileInfo;

            if (videoInfo == null)
            {
                var errorMessage = "Video info not found!";
                Logger.Info(errorMessage);
                return $"RENAMER_ERROR: {errorMessage}";
            }

            var mediaInfo = videoInfo.MediaInfo?.Video;
            var anidbFileInfo = videoInfo.AniDB;

            if (anidbFileInfo == null)
            {
                var errorMessage = "AniDB info not found!";
                Logger.Info(errorMessage);
                return $"RENAMER_ERROR: {errorMessage}";
            }

            // Get the preferred title (aka Overriden title)
            var animeName = animeInfo.PreferredTitle;
            Logger.Info($"Anime Name: {animeName}");

            var episodeTitleOrNumber = GetEpisodeTitleOrNumber(animeInfo, episodeInfo);
            Logger.Info($"Episode Number or Title: {episodeTitleOrNumber}");

            var resolution = "";
            try
            {
                resolution = $"{mediaInfo!.Width}x{mediaInfo!.Height}";
            }
            catch (Exception)
            {
                resolution = Regex.Match(fileInfo.FileName, @"\d+x\d+").Value;
            }
            Logger.Info($"Resolution: {resolution}");

            var codec = "";
            try
            {
                codec = mediaInfo!.SimplifiedCodec;
            }
            catch (Exception)
            {
                codec = fileInfo.FileName.Contains("HEVC") ? "HEVC" : "H264";
            }
            Logger.Info($"Codec: {codec}");

            var source = anidbFileInfo.Source;
            Logger.Info($"Source: {source}");

            if (source.Contains("TV")) source = " TV";
            else if (source.Contains("DVD")) source = " DVD";
            else
                source = source switch
                {
                    "BluRay" => " BD",
                    "Web" => " Web",
                    _ => ""
                };
            Logger.Info($"Simplified source: {source}");

            var crc = videoInfo.Hashes!.CRC;
            Logger.Info($"CRC: {crc}");

            var releaseGroup = anidbFileInfo.ReleaseGroup.ShortName;
            Logger.Info($"Release Group: {releaseGroup}");

            var ext = Path.GetExtension(fileInfo.FileName);

            // build a string like "Tokyo Revengers - 24 (1920x1080 HEVC BD) (95624E85) [Hi10].mkv"
            var result = $"{animeName} - {episodeTitleOrNumber} ({resolution} {codec}{source}) ({crc}) [{releaseGroup}]{ext}";

            // Remove invalid characters
            result = result.ReplaceInvalidPathCharacters();

            return result;
        }

        private static string GetEpisodeTitleOrNumber(ISeries animeInfo, List<IEpisode> episodesInfo)
        {
            string episodeTitleOrNumber = null;

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

        private static string GetEpisodeNumber(IEpisode episodeInfo, ISeries animeInfo)
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
