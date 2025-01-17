﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Interfaces.Repositories;
using ErsatzTV.Core.Jellyfin;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.EntityFrameworkCore;

namespace ErsatzTV.Infrastructure.Data.Repositories
{
    public class JellyfinTelevisionRepository : IJellyfinTelevisionRepository
    {
        private readonly IDbConnection _dbConnection;
        private readonly IDbContextFactory<TvContext> _dbContextFactory;

        public JellyfinTelevisionRepository(IDbConnection dbConnection, IDbContextFactory<TvContext> dbContextFactory)
        {
            _dbConnection = dbConnection;
            _dbContextFactory = dbContextFactory;
        }

        public Task<List<JellyfinItemEtag>> GetExistingShows(JellyfinLibrary library) =>
            _dbConnection.QueryAsync<JellyfinItemEtag>(
                    @"SELECT ItemId, Etag FROM JellyfinShow
                      INNER JOIN Show S on JellyfinShow.Id = S.Id
                      INNER JOIN MediaItem MI on S.Id = MI.Id
                      INNER JOIN LibraryPath LP on MI.LibraryPathId = LP.Id
                      WHERE LP.LibraryId = @LibraryId",
                    new { LibraryId = library.Id })
                .Map(result => result.ToList());

        public Task<List<JellyfinItemEtag>> GetExistingSeasons(JellyfinLibrary library, string showItemId) =>
            _dbConnection.QueryAsync<JellyfinItemEtag>(
                    @"SELECT JellyfinSeason.ItemId, JellyfinSeason.Etag FROM JellyfinSeason
                      INNER JOIN Season S on JellyfinSeason.Id = S.Id
                      INNER JOIN MediaItem MI on S.Id = MI.Id
                      INNER JOIN LibraryPath LP on MI.LibraryPathId = LP.Id
                      INNER JOIN Show S2 on S.ShowId = S2.Id
                      INNER JOIN JellyfinShow JS on S2.Id = JS.Id
                      WHERE LP.LibraryId = @LibraryId AND JS.ItemId = @ShowItemId",
                    new { LibraryId = library.Id, ShowItemId = showItemId })
                .Map(result => result.ToList());

        public Task<List<JellyfinItemEtag>> GetExistingEpisodes(JellyfinLibrary library, string seasonItemId) =>
            _dbConnection.QueryAsync<JellyfinItemEtag>(
                    @"SELECT JellyfinEpisode.ItemId, JellyfinEpisode.Etag FROM JellyfinEpisode
                      INNER JOIN Episode E on JellyfinEpisode.Id = E.Id
                      INNER JOIN MediaItem MI on E.Id = MI.Id
                      INNER JOIN LibraryPath LP on MI.LibraryPathId = LP.Id
                      INNER JOIN Season S2 on E.SeasonId = S2.Id
                      INNER JOIN JellyfinSeason JS on S2.Id = JS.Id
                      WHERE LP.LibraryId = @LibraryId AND JS.ItemId = @SeasonItemId",
                    new { LibraryId = library.Id, SeasonItemId = seasonItemId })
                .Map(result => result.ToList());

        public async Task<bool> AddShow(JellyfinShow show)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            await dbContext.AddAsync(show);
            if (await dbContext.SaveChangesAsync() <= 0)
            {
                return false;
            }

            await dbContext.Entry(show).Reference(m => m.LibraryPath).LoadAsync();
            await dbContext.Entry(show.LibraryPath).Reference(lp => lp.Library).LoadAsync();
            return true;
        }

        public async Task<Option<JellyfinShow>> Update(JellyfinShow show)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            Option<JellyfinShow> maybeExisting = await dbContext.JellyfinShows
                .Include(m => m.LibraryPath)
                .ThenInclude(lp => lp.Library)
                .Include(m => m.ShowMetadata)
                .ThenInclude(mm => mm.Genres)
                .Include(m => m.ShowMetadata)
                .ThenInclude(mm => mm.Tags)
                .Include(m => m.ShowMetadata)
                .ThenInclude(mm => mm.Studios)
                .Include(m => m.ShowMetadata)
                .ThenInclude(mm => mm.Actors)
                .Include(m => m.ShowMetadata)
                .ThenInclude(mm => mm.Artwork)
                .Include(m => m.ShowMetadata)
                .ThenInclude(mm => mm.Guids)
                .Include(m => m.TraktListItems)
                .ThenInclude(tli => tli.TraktList)
                .Filter(m => m.ItemId == show.ItemId)
                .OrderBy(m => m.ItemId)
                .SingleOrDefaultAsync();

            if (maybeExisting.IsSome)
            {
                JellyfinShow existing = maybeExisting.ValueUnsafe();

                // library path is used for search indexing later
                show.LibraryPath = existing.LibraryPath;
                show.Id = existing.Id;

                existing.Etag = show.Etag;

                // metadata
                ShowMetadata metadata = existing.ShowMetadata.Head();
                ShowMetadata incomingMetadata = show.ShowMetadata.Head();
                metadata.MetadataKind = incomingMetadata.MetadataKind;
                metadata.ContentRating = incomingMetadata.ContentRating;
                metadata.Title = incomingMetadata.Title;
                metadata.SortTitle = incomingMetadata.SortTitle;
                metadata.Plot = incomingMetadata.Plot;
                metadata.Year = incomingMetadata.Year;
                metadata.Tagline = incomingMetadata.Tagline;
                metadata.DateAdded = incomingMetadata.DateAdded;
                metadata.DateUpdated = DateTime.UtcNow;

                // genres
                foreach (Genre genre in metadata.Genres
                    .Filter(g => incomingMetadata.Genres.All(g2 => g2.Name != g.Name))
                    .ToList())
                {
                    metadata.Genres.Remove(genre);
                }

                foreach (Genre genre in incomingMetadata.Genres
                    .Filter(g => metadata.Genres.All(g2 => g2.Name != g.Name))
                    .ToList())
                {
                    metadata.Genres.Add(genre);
                }

                // tags
                foreach (Tag tag in metadata.Tags
                    .Filter(g => incomingMetadata.Tags.All(g2 => g2.Name != g.Name))
                    .ToList())
                {
                    metadata.Tags.Remove(tag);
                }

                foreach (Tag tag in incomingMetadata.Tags
                    .Filter(g => metadata.Tags.All(g2 => g2.Name != g.Name))
                    .ToList())
                {
                    metadata.Tags.Add(tag);
                }

                // studios
                foreach (Studio studio in metadata.Studios
                    .Filter(g => incomingMetadata.Studios.All(g2 => g2.Name != g.Name))
                    .ToList())
                {
                    metadata.Studios.Remove(studio);
                }

                foreach (Studio studio in incomingMetadata.Studios
                    .Filter(g => metadata.Studios.All(g2 => g2.Name != g.Name))
                    .ToList())
                {
                    metadata.Studios.Add(studio);
                }

                // actors
                foreach (Actor actor in metadata.Actors
                    .Filter(
                        a => incomingMetadata.Actors.All(
                            a2 => a2.Name != a.Name || a.Artwork == null && a2.Artwork != null))
                    .ToList())
                {
                    metadata.Actors.Remove(actor);
                }

                foreach (Actor actor in incomingMetadata.Actors
                    .Filter(a => metadata.Actors.All(a2 => a2.Name != a.Name))
                    .ToList())
                {
                    metadata.Actors.Add(actor);
                }

                // guids
                foreach (MetadataGuid guid in metadata.Guids
                    .Filter(g => incomingMetadata.Guids.All(g2 => g2.Guid != g.Guid))
                    .ToList())
                {
                    metadata.Guids.Remove(guid);
                }

                foreach (MetadataGuid guid in incomingMetadata.Guids
                    .Filter(g => metadata.Guids.All(g2 => g2.Guid != g.Guid))
                    .ToList())
                {
                    metadata.Guids.Add(guid);
                }

                metadata.ReleaseDate = incomingMetadata.ReleaseDate;

                // poster
                Artwork incomingPoster =
                    incomingMetadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Poster);
                if (incomingPoster != null)
                {
                    Artwork poster = metadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Poster);
                    if (poster == null)
                    {
                        poster = new Artwork { ArtworkKind = ArtworkKind.Poster };
                        metadata.Artwork.Add(poster);
                    }

                    poster.Path = incomingPoster.Path;
                    poster.DateAdded = incomingPoster.DateAdded;
                    poster.DateUpdated = incomingPoster.DateUpdated;
                }

                // thumbnail
                Artwork incomingThumbnail =
                    incomingMetadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Thumbnail);
                if (incomingThumbnail != null)
                {
                    Artwork thumb = metadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Thumbnail);
                    if (thumb == null)
                    {
                        thumb = new Artwork { ArtworkKind = ArtworkKind.Thumbnail };
                        metadata.Artwork.Add(thumb);
                    }

                    thumb.Path = incomingThumbnail.Path;
                    thumb.DateAdded = incomingThumbnail.DateAdded;
                    thumb.DateUpdated = incomingThumbnail.DateUpdated;
                }

                // fan art
                Artwork incomingFanArt =
                    incomingMetadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.FanArt);
                if (incomingFanArt != null)
                {
                    Artwork fanArt = metadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.FanArt);
                    if (fanArt == null)
                    {
                        fanArt = new Artwork { ArtworkKind = ArtworkKind.FanArt };
                        metadata.Artwork.Add(fanArt);
                    }

                    fanArt.Path = incomingFanArt.Path;
                    fanArt.DateAdded = incomingFanArt.DateAdded;
                    fanArt.DateUpdated = incomingFanArt.DateUpdated;
                }

                var paths = incomingMetadata.Artwork.Map(a => a.Path).ToList();
                foreach (Artwork artworkToRemove in metadata.Artwork.Filter(a => !paths.Contains(a.Path)))
                {
                    metadata.Artwork.Remove(artworkToRemove);
                }
            }

            await dbContext.SaveChangesAsync();

            return maybeExisting;
        }

        public async Task<bool> AddSeason(JellyfinShow show, JellyfinSeason season)
        {
            try
            {
                season.ShowId = await _dbConnection.ExecuteScalarAsync<int>(
                    @"SELECT Id FROM JellyfinShow WHERE ItemId = @ItemId",
                    new { show.ItemId });

                await using TvContext dbContext = _dbContextFactory.CreateDbContext();
                await dbContext.AddAsync(season);
                if (await dbContext.SaveChangesAsync() <= 0)
                {
                    return false;
                }

                await dbContext.Entry(season).Reference(m => m.LibraryPath).LoadAsync();
                await dbContext.Entry(season.LibraryPath).Reference(lp => lp.Library).LoadAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<Option<JellyfinSeason>> Update(JellyfinSeason season)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            Option<JellyfinSeason> maybeExisting = await dbContext.JellyfinSeasons
                .Include(m => m.LibraryPath)
                .Include(m => m.SeasonMetadata)
                .ThenInclude(mm => mm.Artwork)
                .Include(m => m.SeasonMetadata)
                .ThenInclude(mm => mm.Guids)
                .Filter(m => m.ItemId == season.ItemId)
                .OrderBy(m => m.ItemId)
                .SingleOrDefaultAsync();

            if (maybeExisting.IsSome)
            {
                JellyfinSeason existing = maybeExisting.ValueUnsafe();

                // library path is used for search indexing later
                season.LibraryPath = existing.LibraryPath;
                season.Id = existing.Id;

                existing.Etag = season.Etag;
                existing.SeasonNumber = season.SeasonNumber;

                // metadata
                SeasonMetadata metadata = existing.SeasonMetadata.Head();
                SeasonMetadata incomingMetadata = season.SeasonMetadata.Head();
                metadata.Title = incomingMetadata.Title;
                metadata.SortTitle = incomingMetadata.SortTitle;
                metadata.Year = incomingMetadata.Year;
                metadata.DateAdded = incomingMetadata.DateAdded;
                metadata.DateUpdated = DateTime.UtcNow;
                metadata.ReleaseDate = incomingMetadata.ReleaseDate;

                // poster
                Artwork incomingPoster =
                    incomingMetadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Poster);
                if (incomingPoster != null)
                {
                    Artwork poster = metadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Poster);
                    if (poster == null)
                    {
                        poster = new Artwork { ArtworkKind = ArtworkKind.Poster };
                        metadata.Artwork.Add(poster);
                    }

                    poster.Path = incomingPoster.Path;
                    poster.DateAdded = incomingPoster.DateAdded;
                    poster.DateUpdated = incomingPoster.DateUpdated;
                }

                // fan art
                Artwork incomingFanArt =
                    incomingMetadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.FanArt);
                if (incomingFanArt != null)
                {
                    Artwork fanArt = metadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.FanArt);
                    if (fanArt == null)
                    {
                        fanArt = new Artwork { ArtworkKind = ArtworkKind.FanArt };
                        metadata.Artwork.Add(fanArt);
                    }

                    fanArt.Path = incomingFanArt.Path;
                    fanArt.DateAdded = incomingFanArt.DateAdded;
                    fanArt.DateUpdated = incomingFanArt.DateUpdated;
                }

                // guids
                foreach (MetadataGuid guid in metadata.Guids
                    .Filter(g => incomingMetadata.Guids.All(g2 => g2.Guid != g.Guid))
                    .ToList())
                {
                    metadata.Guids.Remove(guid);
                }

                foreach (MetadataGuid guid in incomingMetadata.Guids
                    .Filter(g => metadata.Guids.All(g2 => g2.Guid != g.Guid))
                    .ToList())
                {
                    metadata.Guids.Add(guid);
                }

                var paths = incomingMetadata.Artwork.Map(a => a.Path).ToList();
                foreach (Artwork artworkToRemove in metadata.Artwork.Filter(a => !paths.Contains(a.Path)))
                {
                    metadata.Artwork.Remove(artworkToRemove);
                }

                await dbContext.SaveChangesAsync();
                await dbContext.Entry(existing.LibraryPath).Reference(lp => lp.Library).LoadAsync();
            }

            return maybeExisting;
        }

        public async Task<bool> AddEpisode(JellyfinSeason season, JellyfinEpisode episode)
        {
            try
            {
                episode.SeasonId = await _dbConnection.ExecuteScalarAsync<int>(
                    @"SELECT Id FROM JellyfinSeason WHERE ItemId = @ItemId",
                    new { season.ItemId });

                await using TvContext dbContext = _dbContextFactory.CreateDbContext();
                await dbContext.AddAsync(episode);
                if (await dbContext.SaveChangesAsync() <= 0)
                {
                    return false;
                }

                await dbContext.Entry(episode).Reference(m => m.LibraryPath).LoadAsync();
                await dbContext.Entry(episode.LibraryPath).Reference(lp => lp.Library).LoadAsync();
                await dbContext.Entry(episode).Reference(e => e.Season).LoadAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<Option<JellyfinEpisode>> Update(JellyfinEpisode episode)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            Option<JellyfinEpisode> maybeExisting = await dbContext.JellyfinEpisodes
                .Include(m => m.LibraryPath)
                .ThenInclude(lp => lp.Library)
                .Include(m => m.MediaVersions)
                .ThenInclude(mv => mv.MediaFiles)
                .Include(m => m.MediaVersions)
                .ThenInclude(mv => mv.Streams)
                .Include(m => m.EpisodeMetadata)
                .ThenInclude(mm => mm.Artwork)
                .Include(m => m.EpisodeMetadata)
                .ThenInclude(mm => mm.Guids)
                .Include(m => m.EpisodeMetadata)
                .ThenInclude(mm => mm.Genres)
                .Include(m => m.EpisodeMetadata)
                .ThenInclude(mm => mm.Tags)
                .Include(m => m.EpisodeMetadata)
                .ThenInclude(mm => mm.Studios)
                .Include(m => m.EpisodeMetadata)
                .ThenInclude(mm => mm.Actors)
                .Include(m => m.EpisodeMetadata)
                .ThenInclude(mm => mm.Directors)
                .Include(m => m.EpisodeMetadata)
                .ThenInclude(mm => mm.Writers)
                .Include(m => m.Season)
                .Include(m => m.TraktListItems)
                .ThenInclude(tli => tli.TraktList)
                .Filter(m => m.ItemId == episode.ItemId)
                .OrderBy(m => m.ItemId)
                .SingleOrDefaultAsync();

            if (maybeExisting.IsSome)
            {
                JellyfinEpisode existing = maybeExisting.ValueUnsafe();

                // library path is used for search indexing later
                episode.LibraryPath = existing.LibraryPath;
                episode.Id = existing.Id;

                existing.Etag = episode.Etag;

                // metadata
                // TODO: multiple metadata?
                EpisodeMetadata metadata = existing.EpisodeMetadata.Head();
                EpisodeMetadata incomingMetadata = episode.EpisodeMetadata.Head();
                metadata.Title = incomingMetadata.Title;
                metadata.SortTitle = incomingMetadata.SortTitle;
                metadata.Plot = incomingMetadata.Plot;
                metadata.Year = incomingMetadata.Year;
                metadata.DateAdded = incomingMetadata.DateAdded;
                metadata.DateUpdated = DateTime.UtcNow;
                metadata.ReleaseDate = incomingMetadata.ReleaseDate;
                metadata.EpisodeNumber = metadata.EpisodeNumber;

                // thumbnail
                Artwork incomingThumbnail =
                    incomingMetadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Thumbnail);
                if (incomingThumbnail != null)
                {
                    Artwork thumbnail = metadata.Artwork.FirstOrDefault(a => a.ArtworkKind == ArtworkKind.Thumbnail);
                    if (thumbnail == null)
                    {
                        thumbnail = new Artwork { ArtworkKind = ArtworkKind.Thumbnail };
                        metadata.Artwork.Add(thumbnail);
                    }

                    thumbnail.Path = incomingThumbnail.Path;
                    thumbnail.DateAdded = incomingThumbnail.DateAdded;
                    thumbnail.DateUpdated = incomingThumbnail.DateUpdated;
                }

                // directors
                foreach (Director director in metadata.Directors
                    .Filter(d => incomingMetadata.Directors.All(d2 => d2.Name != d.Name))
                    .ToList())
                {
                    metadata.Directors.Remove(director);
                }

                foreach (Director director in incomingMetadata.Directors
                    .Filter(d => metadata.Directors.All(d2 => d2.Name != d.Name))
                    .ToList())
                {
                    metadata.Directors.Add(director);
                }

                // writers
                foreach (Writer writer in metadata.Writers
                    .Filter(w => incomingMetadata.Writers.All(w2 => w2.Name != w.Name))
                    .ToList())
                {
                    metadata.Writers.Remove(writer);
                }

                foreach (Writer writer in incomingMetadata.Writers
                    .Filter(w => metadata.Writers.All(w2 => w2.Name != w.Name))
                    .ToList())
                {
                    metadata.Writers.Add(writer);
                }

                // guids
                foreach (MetadataGuid guid in metadata.Guids
                    .Filter(g => incomingMetadata.Guids.All(g2 => g2.Guid != g.Guid))
                    .ToList())
                {
                    metadata.Guids.Remove(guid);
                }

                foreach (MetadataGuid guid in incomingMetadata.Guids
                    .Filter(g => metadata.Guids.All(g2 => g2.Guid != g.Guid))
                    .ToList())
                {
                    metadata.Guids.Add(guid);
                }

                var paths = incomingMetadata.Artwork.Map(a => a.Path).ToList();
                foreach (Artwork artworkToRemove in metadata.Artwork.Filter(a => !paths.Contains(a.Path)))
                {
                    metadata.Artwork.Remove(artworkToRemove);
                }

                // version
                MediaVersion version = existing.MediaVersions.Head();
                MediaVersion incomingVersion = episode.MediaVersions.Head();
                version.Name = incomingVersion.Name;
                version.DateAdded = incomingVersion.DateAdded;

                // media file
                MediaFile file = version.MediaFiles.Head();
                MediaFile incomingFile = incomingVersion.MediaFiles.Head();
                file.Path = incomingFile.Path;
            }

            await dbContext.SaveChangesAsync();

            return maybeExisting;
        }

        public async Task<List<int>> RemoveMissingShows(JellyfinLibrary library, List<string> showIds)
        {
            List<int> ids = await _dbConnection.QueryAsync<int>(
                @"SELECT m.Id FROM MediaItem m
                INNER JOIN JellyfinShow js ON js.Id = m.Id
                INNER JOIN LibraryPath lp ON lp.Id = m.LibraryPathId
                WHERE lp.LibraryId = @LibraryId AND js.ItemId IN @ShowIds",
                new { LibraryId = library.Id, ShowIds = showIds }).Map(result => result.ToList());

            await _dbConnection.ExecuteAsync(
                @"DELETE FROM MediaItem WHERE Id IN
                (SELECT m.Id FROM MediaItem m
                INNER JOIN JellyfinShow js ON js.Id = m.Id
                INNER JOIN LibraryPath lp ON lp.Id = m.LibraryPathId
                WHERE lp.LibraryId = @LibraryId AND js.ItemId IN @ShowIds)",
                new { LibraryId = library.Id, ShowIds = showIds });

            return ids;
        }

        public Task<Unit> RemoveMissingSeasons(JellyfinLibrary library, List<string> seasonIds) =>
            _dbConnection.ExecuteAsync(
                @"DELETE FROM MediaItem WHERE Id IN
                (SELECT m.Id FROM MediaItem m
                INNER JOIN JellyfinSeason js ON js.Id = m.Id
                INNER JOIN LibraryPath LP on m.LibraryPathId = LP.Id
                WHERE LP.LibraryId = @LibraryId AND js.ItemId IN @SeasonIds)",
                new { LibraryId = library.Id, SeasonIds = seasonIds }).ToUnit();

        public async Task<List<int>> RemoveMissingEpisodes(JellyfinLibrary library, List<string> episodeIds)
        {
            List<int> ids = await _dbConnection.QueryAsync<int>(
                @"SELECT m.Id FROM MediaItem m
                INNER JOIN JellyfinEpisode je ON je.Id = m.Id
                INNER JOIN LibraryPath lp ON lp.Id = m.LibraryPathId
                WHERE lp.LibraryId = @LibraryId AND je.ItemId IN @EpisodeIds",
                new { LibraryId = library.Id, EpisodeIds = episodeIds }).Map(result => result.ToList());

            await _dbConnection.ExecuteAsync(
                @"DELETE FROM MediaItem WHERE Id IN
                (SELECT m.Id FROM MediaItem m
                INNER JOIN JellyfinEpisode je ON je.Id = m.Id
                INNER JOIN LibraryPath LP on m.LibraryPathId = LP.Id
                WHERE LP.LibraryId = @LibraryId AND je.ItemId IN @EpisodeIds)",
                new { LibraryId = library.Id, EpisodeIds = episodeIds });

            return ids;
        }

        public async Task<Unit> DeleteEmptySeasons(JellyfinLibrary library)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            List<JellyfinSeason> seasons = await dbContext.JellyfinSeasons
                .Filter(s => s.LibraryPath.LibraryId == library.Id)
                .Filter(s => s.Episodes.Count == 0)
                .ToListAsync();
            dbContext.Seasons.RemoveRange(seasons);
            await dbContext.SaveChangesAsync();
            return Unit.Default;
        }

        public async Task<List<int>> DeleteEmptyShows(JellyfinLibrary library)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            List<JellyfinShow> shows = await dbContext.JellyfinShows
                .Filter(s => s.LibraryPath.LibraryId == library.Id)
                .Filter(s => s.Seasons.Count == 0)
                .ToListAsync();
            var ids = shows.Map(s => s.Id).ToList();
            dbContext.Shows.RemoveRange(shows);
            await dbContext.SaveChangesAsync();
            return ids;
        }
    }
}
