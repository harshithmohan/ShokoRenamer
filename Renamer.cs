using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Renamer
{
    [Renamer("CustomRenamer", Description = "My custom renamer")]
    public class Renamer : IRenamer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string GetFilename(RenameEventArgs args)
        {
            var animeInfo = args.AnimeInfo.FirstOrDefault();

            // Get the preferred title (Overriden, as shown in Desktop)
            var animeName = animeInfo?.PreferredTitle;
            
            if (string.IsNullOrEmpty(animeName))
            {
                Logger.Info("Anime name not found!");
                args.Cancel = true;
                return null;
            }
            Logger.Info($"Anime Name: {animeName}");

            var allEpisodesInfo = args.EpisodeInfo;

            var firstEpisodeInfo = allEpisodesInfo.First();
            allEpisodesInfo.RemoveAt(0);

            string episodeTitle = null;
            string episodeTitleOrNumber = null;
            
            if (animeInfo.Type == AnimeType.Movie || firstEpisodeInfo.Type != EpisodeType.Episode)
            {
                episodeTitle = firstEpisodeInfo.Titles.FirstOrDefault(title =>
                    title.Language == TitleLanguage.English)?.Title;

                foreach (var otherEpisodeInfo in allEpisodesInfo)
                {
                    if (firstEpisodeInfo.Type == otherEpisodeInfo.Type)
                    {
                        episodeTitle += ", " + otherEpisodeInfo.Titles.FirstOrDefault(title =>
                            title.Language == TitleLanguage.English)?.Title;
                    }
                }

                episodeTitleOrNumber = episodeTitle;
            }
            
            if (animeInfo.Type != AnimeType.Movie)
            {
                var paddedEpisodeNumber = GetEpisodeNumber(firstEpisodeInfo, animeInfo);

                foreach (var otherEpisodeInfo in allEpisodesInfo)
                {
                    if (firstEpisodeInfo.Type == otherEpisodeInfo.Type)
                        paddedEpisodeNumber += '-' + GetEpisodeNumber(otherEpisodeInfo, animeInfo);
                }

                episodeTitleOrNumber = firstEpisodeInfo.Type != EpisodeType.Episode
                    ? $"{paddedEpisodeNumber} - {episodeTitle}"
                    : paddedEpisodeNumber;
            }

            Logger.Info($"Episode Number or Title: {episodeTitleOrNumber}");
            
            var fileInfo = args.FileInfo;

            var videoInfo = fileInfo.MediaInfo.Video;

            var resolution = "";
            try
            {
                resolution = videoInfo.Width.ToString() + 'x' + videoInfo.Height.ToString();
            }
            catch (Exception)
            {
                resolution = Regex.Match(fileInfo.Filename, @"\d+x\d+").Value;
            }

            Logger.Info($"Resolution: {resolution}");

            var codec = "";
            try
            {
                codec = videoInfo.SimplifiedCodec;
            }
            catch (Exception)
            {
                codec = fileInfo.Filename.Contains("HEVC") ? "HEVC" : "H264";
            }

            Logger.Info($"Codec: {codec}");

            var source = fileInfo.AniDBFileInfo.Source;

            Logger.Info($"Source: {source}");

            if (source.Contains("TV")) source = "TV";
            else if (source.Contains("DVD")) source = "DVD";
            else
                source = source switch
                {
                    "BluRay" => "BD",
                    "Web" => "Web",
                    _ => ""
                };

            Logger.Info($"Simplified source: {source}");
            
            var crc = fileInfo.Hashes.CRC;
            
            Logger.Info($"CRC: {crc}");

            var releaseGroup = fileInfo.AniDBFileInfo.ReleaseGroup.ShortName;
            Logger.Info($"Release Group: {releaseGroup}");
            
            var ext = Path.GetExtension(fileInfo.Filename);

            // build a string like "Tokyo Revengers - 24 (1920x1080 HEVC BD) (95624E85) [Hi10].mkv"
            var result = $"{animeName} - {episodeTitleOrNumber} ({resolution} {codec} {source}) ({crc}) [{releaseGroup}]{ext}";

            // Remove invalid characters
            result = result.ReplaceInvalidPathCharacters();

            return result;
        }

        private static string GetEpisodeNumber(IEpisode episodeInfo, IAnime animeInfo)
        {
            return episodeInfo.Type switch
            {
                EpisodeType.Episode => episodeInfo.Number.PadZeroes(Math.Max(animeInfo.EpisodeCounts.Episodes, 10)),
                EpisodeType.Credits => "C" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Credits),
                EpisodeType.Special => "S" + episodeInfo.Number.PadZeroes(Math.Max(animeInfo.EpisodeCounts.Specials, 10)),
                EpisodeType.Trailer => "T" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Trailers),
                EpisodeType.Parody => "P" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Parodies),
                EpisodeType.Other => "O" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Others),
                _ => null
            };
        }

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            // Note: ReplaceInvalidPathCharacters() replaces things like slashes, pluses, etc with Unicode that looks similar

            // Get the first available import folder that is a drop destination
            var destination = args.AvailableFolders.First(a => a.DropFolderType.HasFlag(DropFolderType.Destination));
            
            // Get Anime Info
            var animeInfo = args.AnimeInfo;

            if (animeInfo.Count == 0)
            {
                Logger.Info("Anime name not found!");
                args.Cancel = true;
                return (null, null);
            }
            
            // Get the preferred title (Overriden, as shown in Desktop)
            var animeName = animeInfo.First().PreferredTitle.ReplaceInvalidPathCharacters();
            if (animeName.Contains("Toriko"))
            {
                animeName = args.AnimeInfo.Last().PreferredTitle.ReplaceInvalidPathCharacters();
            }

            return (destination, animeName);
        }
    }
}
