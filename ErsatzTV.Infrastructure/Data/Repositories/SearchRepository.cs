﻿using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Interfaces.Repositories;
using ErsatzTV.Infrastructure.Extensions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace ErsatzTV.Infrastructure.Data.Repositories
{
    public class SearchRepository : ISearchRepository
    {
        private readonly IDbConnection _dbConnection;
        private readonly IDbContextFactory<TvContext> _dbContextFactory;

        public SearchRepository(IDbContextFactory<TvContext> dbContextFactory, IDbConnection dbConnection)
        {
            _dbContextFactory = dbContextFactory;
            _dbConnection = dbConnection;
        }

        public Task<List<int>> GetItemIdsToIndex() =>
            _dbConnection.QueryAsync<int>(@"SELECT Id FROM MediaItem")
                .Map(result => result.ToList());

        public async Task<Option<MediaItem>> GetItemToIndex(int id)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            return await dbContext.MediaItems
                .AsNoTracking()
                .Include(mi => mi.LibraryPath)
                .ThenInclude(lp => lp.Library)
                .Include(mi => (mi as Movie).MovieMetadata)
                .ThenInclude(mm => mm.Genres)
                .Include(mi => (mi as Movie).MovieMetadata)
                .ThenInclude(mm => mm.Tags)
                .Include(mi => (mi as Movie).MovieMetadata)
                .ThenInclude(mm => mm.Studios)
                .Include(mi => (mi as Movie).MovieMetadata)
                .ThenInclude(mm => mm.Actors)
                .Include(mi => (mi as Movie).MovieMetadata)
                .ThenInclude(mm => mm.Directors)
                .Include(mi => (mi as Movie).MovieMetadata)
                .ThenInclude(mm => mm.Writers)
                .Include(mi => (mi as Movie).MediaVersions)
                .ThenInclude(mm => mm.Streams)
                .Include(mi => (mi as Episode).EpisodeMetadata)
                .ThenInclude(em => em.Genres)
                .Include(mi => (mi as Episode).EpisodeMetadata)
                .ThenInclude(em => em.Tags)
                .Include(mi => (mi as Episode).EpisodeMetadata)
                .ThenInclude(em => em.Studios)
                .Include(mi => (mi as Episode).EpisodeMetadata)
                .ThenInclude(em => em.Actors)
                .Include(mi => (mi as Episode).EpisodeMetadata)
                .ThenInclude(em => em.Directors)
                .Include(mi => (mi as Episode).EpisodeMetadata)
                .ThenInclude(em => em.Writers)
                .Include(mi => (mi as Episode).EpisodeMetadata)
                .ThenInclude(em => em.Guids)
                .Include(mi => (mi as Episode).MediaVersions)
                .ThenInclude(em => em.Streams)
                .Include(mi => (mi as Episode).Season)
                .Include(mi => (mi as Season).SeasonMetadata)
                .ThenInclude(sm => sm.Genres)
                .Include(mi => (mi as Season).SeasonMetadata)
                .ThenInclude(sm => sm.Tags)
                .Include(mi => (mi as Season).SeasonMetadata)
                .ThenInclude(sm => sm.Studios)
                .Include(mi => (mi as Season).SeasonMetadata)
                .ThenInclude(sm => sm.Actors)
                .Include(mi => (mi as Season).Show)
                .ThenInclude(sm => sm.ShowMetadata)
                .Include(mi => (mi as Show).ShowMetadata)
                .ThenInclude(mm => mm.Genres)
                .Include(mi => (mi as Show).ShowMetadata)
                .ThenInclude(mm => mm.Tags)
                .Include(mi => (mi as Show).ShowMetadata)
                .ThenInclude(mm => mm.Studios)
                .Include(mi => (mi as Show).ShowMetadata)
                .ThenInclude(mm => mm.Actors)
                .Include(mi => (mi as MusicVideo).MusicVideoMetadata)
                .ThenInclude(mm => mm.Genres)
                .Include(mi => (mi as MusicVideo).MusicVideoMetadata)
                .ThenInclude(mm => mm.Tags)
                .Include(mi => (mi as MusicVideo).MusicVideoMetadata)
                .ThenInclude(mm => mm.Studios)
                .Include(mi => (mi as MusicVideo).MediaVersions)
                .ThenInclude(mm => mm.Streams)
                .Include(mi => (mi as Artist).ArtistMetadata)
                .ThenInclude(mm => mm.Genres)
                .Include(mi => (mi as Artist).ArtistMetadata)
                .ThenInclude(mm => mm.Styles)
                .Include(mi => (mi as Artist).ArtistMetadata)
                .ThenInclude(mm => mm.Moods)
                .Include(mi => mi.TraktListItems)
                .ThenInclude(tli => tli.TraktList)
                .OrderBy(mi => mi.Id)
                .SingleOrDefaultAsync(mi => mi.Id == id)
                .Map(Optional);
        }

        public Task<List<string>> GetLanguagesForShow(Show show) =>
            _dbConnection.QueryAsync<string>(
                @"SELECT DISTINCT Language
                    FROM MediaStream
                    INNER JOIN MediaVersion MV ON MediaStream.MediaVersionId = MV.Id
                    INNER JOIN Episode E ON MV.EpisodeId = E.Id
                    INNER JOIN Season S ON E.SeasonId = S.Id
                    WHERE MediaStreamKind = 2 AND S.ShowId = @ShowId",
                new { ShowId = show.Id }).Map(result => result.ToList());

        public Task<List<string>> GetLanguagesForSeason(Season season) =>
            _dbConnection.QueryAsync<string>(
                @"SELECT DISTINCT Language
                    FROM MediaStream
                    INNER JOIN MediaVersion MV ON MediaStream.MediaVersionId = MV.Id
                    INNER JOIN Episode E ON MV.EpisodeId = E.Id
                    WHERE MediaStreamKind = 2 AND E.SeasonId = @SeasonId",
                new { SeasonId = season.Id }).Map(result => result.ToList());

        public Task<List<string>> GetLanguagesForArtist(Artist artist) =>
            _dbConnection.QueryAsync<string>(
                @"SELECT DISTINCT Language
                    FROM MediaStream
                    INNER JOIN MediaVersion V ON MediaStream.MediaVersionId = V.Id
                    INNER JOIN MusicVideo MV ON V.MusicVideoId = MV.Id
                    INNER JOIN Artist A on MV.ArtistId = A.Id
                    WHERE MediaStreamKind = 2 AND A.Id = @ArtistId",
                new { ArtistId = artist.Id }).Map(result => result.ToList());
        
        public async Task<List<string>> GetAllLanguageCodes(List<string> mediaCodes)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            return await dbContext.LanguageCodes.GetAllLanguageCodes(mediaCodes);
        }
    }
}
