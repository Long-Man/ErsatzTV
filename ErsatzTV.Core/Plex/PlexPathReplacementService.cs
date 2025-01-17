﻿using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Interfaces.Plex;
using ErsatzTV.Core.Interfaces.Repositories;
using ErsatzTV.Core.Interfaces.Runtime;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ErsatzTV.Core.Plex
{
    public class PlexPathReplacementService : IPlexPathReplacementService
    {
        private readonly ILogger<PlexPathReplacementService> _logger;
        private readonly IMediaSourceRepository _mediaSourceRepository;
        private readonly IRuntimeInfo _runtimeInfo;

        public PlexPathReplacementService(
            IMediaSourceRepository mediaSourceRepository,
            IRuntimeInfo runtimeInfo,
            ILogger<PlexPathReplacementService> logger)
        {
            _mediaSourceRepository = mediaSourceRepository;
            _runtimeInfo = runtimeInfo;
            _logger = logger;
        }

        public async Task<string> GetReplacementPlexPath(int libraryPathId, string path)
        {
            List<PlexPathReplacement> replacements =
                await _mediaSourceRepository.GetPlexPathReplacementsByLibraryId(libraryPathId);

            return GetReplacementPlexPath(replacements, path);
        }

        public string GetReplacementPlexPath(List<PlexPathReplacement> pathReplacements, string path, bool log = true)
        {
            Option<PlexPathReplacement> maybeReplacement = pathReplacements
                .SingleOrDefault(
                    r =>
                    {
                        string separatorChar = IsWindows(r.PlexMediaSource) ? @"\" : @"/";
                        string prefix = r.PlexPath.EndsWith(separatorChar) ? r.PlexPath : r.PlexPath + separatorChar;
                        return path.StartsWith(prefix);
                    });

            return maybeReplacement.Match(
                replacement =>
                {
                    string finalPath = path.Replace(replacement.PlexPath, replacement.LocalPath);
                    if (IsWindows(replacement.PlexMediaSource) && !_runtimeInfo.IsOSPlatform(OSPlatform.Windows))
                    {
                        finalPath = finalPath.Replace(@"\", @"/");
                    }
                    else if (!IsWindows(replacement.PlexMediaSource) && _runtimeInfo.IsOSPlatform(OSPlatform.Windows))
                    {
                        finalPath = finalPath.Replace(@"/", @"\");
                    }

                    if (log)
                    {
                        _logger.LogInformation(
                            "Replacing plex path {PlexPath} with {LocalPath} resulting in {FinalPath}",
                            replacement.PlexPath,
                            replacement.LocalPath,
                            finalPath);
                    }

                    return finalPath;
                },
                () => path);
        }

        private static bool IsWindows(PlexMediaSource plexMediaSource) =>
            plexMediaSource.Platform.ToLowerInvariant() == "windows";
    }
}
