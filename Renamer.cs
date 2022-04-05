using System;
using System.Collections.Generic;
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
            // Get the Anime Info
            IAnime animeInfo = args.AnimeInfo.FirstOrDefault();
            IAnime wrongInfo = null;

            if (animeInfo != null && animeInfo.PreferredTitle.Contains("Toriko"))
            {
                wrongInfo = animeInfo;
                animeInfo = args.AnimeInfo.LastOrDefault();
            }

            // Get the preferred title (Overriden, as shown in Desktop)
            string animeName = animeInfo?.PreferredTitle;
            
            // Filenames must be consistent (because OCD), so cancel and return if we can't make a consistent filename style
            if (string.IsNullOrEmpty(animeName))
            {
                Logger.Info("Anime name not found!");
                args.Cancel = true;
                return null;
            }
            Logger.Info($"Anime Name: {animeName}");

            // Get the episode info
            IList<IEpisode> allEpisodesInfo = args.EpisodeInfo;

            if (wrongInfo != null && wrongInfo.PreferredTitle.Contains("Toriko"))
            {
                allEpisodesInfo.RemoveAt(0);
            }
            
            IEpisode firstEpisodeInfo = allEpisodesInfo.First();
            allEpisodesInfo.RemoveAt(0);

            string episodeTitle = null;
            string episodeTitleOrNumber = null;
            
            if (animeInfo.Type == AnimeType.Movie || firstEpisodeInfo.Type != EpisodeType.Episode)
            {
                episodeTitle = firstEpisodeInfo.Titles.FirstOrDefault(title =>
                    title.Language == TitleLanguage.English)?.Title;
                
                foreach (IEpisode otherEpisodeInfo in allEpisodesInfo)
                {
                    episodeTitle += ", " + otherEpisodeInfo.Titles.FirstOrDefault(title =>
                        title.Language == TitleLanguage.English)?.Title;
                }

                episodeTitleOrNumber = episodeTitle;
            }
            
            if (animeInfo.Type != AnimeType.Movie)
            {
                string paddedEpisodeNumber = GetEpisodeNumber(firstEpisodeInfo, animeInfo);

                foreach (IEpisode otherEpisodeInfo in allEpisodesInfo)
                {
                    paddedEpisodeNumber += '-' + GetEpisodeNumber(otherEpisodeInfo, animeInfo);
                }
                
                // Add title if it's not of type "Episode"
                if (firstEpisodeInfo.Type != EpisodeType.Episode)
                {
                    episodeTitleOrNumber = paddedEpisodeNumber + " - " + episodeTitle;
                }
                else
                {
                    episodeTitleOrNumber = paddedEpisodeNumber;
                }
            }

            Logger.Info($"Episode Number or Title: {episodeTitleOrNumber}");
            
            // Get the info about the file
            IVideoFile fileInfo = args.FileInfo;

            // Get the info about the video stream from the MediaInfo
            IVideoStream videoInfo = fileInfo.MediaInfo.Video;

            // Get the resolution
            string resolution = "";
            try
            {
                resolution = videoInfo.Width.ToString() + 'x' + videoInfo.Height.ToString();
            }
            catch (Exception)
            {
                resolution = Regex.Match(fileInfo.Filename, @"\d+x\d+").Value;
            }

            Logger.Info($"Resolution: {resolution}");

            // Get the codec
            string codec = "";
            try
            {
                codec = videoInfo.SimplifiedCodec;
            }
            catch (Exception)
            {
                codec = fileInfo.Filename.Contains("HEVC") ? "HEVC" : "H264";
            }
            
            Logger.Info($"Codec: {codec}");
            
            // Get the CRC hash
            string crc = fileInfo.Hashes.CRC;
            
            Logger.Info($"CRC: {crc}");
            
            // Get the release group short name
            string releaseGroup = fileInfo.AniDBFileInfo.ReleaseGroup.ShortName;
            Logger.Info($"Release Group: {releaseGroup}");
            
            // Get the extension of the original filename, it includes the .
            string ext = Path.GetExtension(fileInfo.Filename);

            // The $ allows building a string with the squiggle brackets
            // build a string like "Boku no Hero Academia - 04 (1920x1080 H264) (6B361564) [Hi10].mkv"
            string result = $"{animeName} - {episodeTitleOrNumber} ({resolution} {codec}) ({crc}) [{releaseGroup}]{ext}";

            // Remove invalid characters
            result = result.ReplaceInvalidPathCharacters();

            // Return the result
            return result;
        }

        private string GetEpisodeNumber(IEpisode episodeInfo, IAnime animeInfo)
        {
            switch (episodeInfo.Type)
            {
                case EpisodeType.Episode:
                    return episodeInfo.Number.PadZeroes(Math.Max(animeInfo.EpisodeCounts.Episodes, 2));
                case EpisodeType.Credits:
                    return "C" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Credits);
                case EpisodeType.Special:
                    return "S" + episodeInfo.Number.PadZeroes(Math.Max(animeInfo.EpisodeCounts.Episodes, 2));
                case EpisodeType.Trailer:
                    return "T" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Trailers);
                case EpisodeType.Parody:
                    return "P" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Parodies);
                case EpisodeType.Other:
                    return "O" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Others);
                default:
                    return null;
            }
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
            var animeName = animeInfo.First().PreferredTitle.RemoveInvalidPathCharacters();
            if (animeName.Contains("Toriko"))
            {
                animeName = args.AnimeInfo.Last().PreferredTitle.RemoveInvalidPathCharacters();
            }

            return (destination, animeName);
        }
    }
}
