using System;
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
                IEpisode episodeInfo = args.EpisodeInfo.First();

                string paddedEpisodeNumber = null;
                switch (episodeInfo.Type)
                {
                    case EpisodeType.Episode:
                        paddedEpisodeNumber = episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Episodes);
                        break;
                    case EpisodeType.Credits:
                        paddedEpisodeNumber = "C" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Credits);
                        break;
                    case EpisodeType.Special:
                        paddedEpisodeNumber = "S" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Specials);
                        break;
                    case EpisodeType.Trailer:
                        paddedEpisodeNumber = "T" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Trailers);
                        break;
                    case EpisodeType.Parody:
                        paddedEpisodeNumber = "P" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Parodies);
                        break;
                    case EpisodeType.Other:
                        paddedEpisodeNumber = "O" + episodeInfo.Number.PadZeroes(animeInfo.EpisodeCounts.Others);
                        break;
                }
                
                Logger.Info($"Padded Episode Number: {paddedEpisodeNumber}");
                
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
                string result = $"{animeName} - {paddedEpisodeNumber} ({resolution} {videoInfo.CodecID.Split('/').LastOrDefault()?.Replace("AVC", "H264")}) ({crc}) [{releaseGroup}]{ext}";

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