using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Renamer
{
    public class Renamer : IRenamer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Gets the current filename of the DLL (simplified)
        public string Name => Assembly.GetExecutingAssembly().GetName().Name;

        public void Load()
        {
            // ignore. We are a renamer
        }

        public void OnSettingsLoaded(IPluginSettings settings)
        {
            // Save this for later.
            // Settings = settings as Settings;
        }

        public void GetFilename(RenameEventArgs args)
        {
            try
            {
                // Get the Anime Info
                IAnime animeInfo = args.AnimeInfo.FirstOrDefault();
                
                // Get the preferred title (Overriden, as shown in Desktop)
                string animeName = animeInfo?.PreferredTitle;
                
                // Filenames must be consistent (because OCD), so cancel and return if we can't make a consistent filename style
                if (string.IsNullOrEmpty(animeName))
                {
                    args.Cancel = true;
                    return;
                }
                Logger.Info($"Anime Name: {animeName}");

                // Get the episode info
                IList<IEpisode> allEpisodesInfo = args.EpisodeInfo;
                IEpisode firstEpisodeInfo = allEpisodesInfo.First();
                allEpisodesInfo.RemoveAt(0);

                string episodeTitle = null;
                string episodeTitleOrNumber = null;
                
                if (animeInfo.Type == AnimeType.Movie || firstEpisodeInfo.Type != EpisodeType.Episode)
                {
                    episodeTitle = firstEpisodeInfo.Titles.FirstOrDefault(title =>
                        title.Language == TitleLanguage.English && title.Type == TitleType.Main)?.Title;
                    
                    foreach (IEpisode otherEpisodeInfo in allEpisodesInfo)
                    {
                        episodeTitle += ", " + otherEpisodeInfo.Titles.FirstOrDefault(title =>
                            title.Language == TitleLanguage.English && title.Type == TitleType.Main)?.Title;
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
                        episodeTitleOrNumber = paddedEpisodeNumber + '-' + episodeTitle;
                    }
                }

                Logger.Info($"Episode Number or Title: {episodeTitleOrNumber}");
                
                // Get the info about the file
                IVideoFile fileInfo = args.FileInfo;
                
                // Get the info about the video stream from the MediaInfo
                IVideoStream videoInfo = fileInfo.MediaInfo.Video;

                // Get the resolution
                string resolution = videoInfo.Width.ToString() + 'x' + videoInfo.Height.ToString();
                
                Logger.Info($"Resolution: {resolution}");

                // Get the codec
                string codec = videoInfo.SimplifiedCodec;
                
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
                string result = $"{animeName} - {episodeTitleOrNumber} ({resolution} {videoInfo.CodecID.Split('/').LastOrDefault()?.Replace("AVC", "H264")}) ({crc}) [{releaseGroup}]{ext}";

                // Remove invalid characters
                result = result.ReplaceInvalidPathCharacters();

                // Set the result
                args.Result = result;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Unable to get new filename for {args.FileInfo?.Filename}");
            }
        }

        private string GetEpisodeNumber(IEpisode episodeInfo, IAnime animeInfo)
        {
            switch (episodeInfo.Type)
            {
                case EpisodeType.Episode:
                    return episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Episodes);
                case EpisodeType.Credits:
                    return "C" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Credits);
                case EpisodeType.Special:
                    return "S" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Specials);
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

        public void GetDestination(MoveEventArgs args)
        {
            try
            {
                // Note: ReplaceInvalidPathCharacters() replaces things like slashes, pluses, etc with Unicode that looks similar

                // Get the first available import folder that is a drop destination
                args.DestinationImportFolder =
                    args.AvailableFolders.First(a => a.DropFolderType.HasFlag(DropFolderType.Destination));

                // Get the preferred title (Overriden, as shown in Desktop)
                string animeName = args.AnimeInfo.First().PreferredTitle.ReplaceInvalidPathCharacters();
                
                args.DestinationPath = animeName;
            }
            catch (Exception e)
            {
                // Log the error to Server
                Logger.Error(e, $"Unable to get destination for {args.FileInfo?.Filename}");
            }
        }
    }
}