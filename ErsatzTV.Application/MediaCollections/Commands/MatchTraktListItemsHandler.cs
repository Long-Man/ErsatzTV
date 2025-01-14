﻿using System.Threading;
using System.Threading.Tasks;
using ErsatzTV.Core;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Interfaces.Locking;
using ErsatzTV.Core.Interfaces.Repositories;
using ErsatzTV.Core.Interfaces.Search;
using ErsatzTV.Core.Interfaces.Trakt;
using ErsatzTV.Infrastructure.Data;
using LanguageExt;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace ErsatzTV.Application.MediaCollections.Commands
{
    public class MatchTraktListItemsHandler : TraktCommandBase,
        IRequestHandler<MatchTraktListItems, Either<BaseError, Unit>>
    {
        private readonly IDbContextFactory<TvContext> _dbContextFactory;
        private readonly IEntityLocker _entityLocker;

        public MatchTraktListItemsHandler(
            ITraktApiClient traktApiClient,
            ISearchRepository searchRepository,
            ISearchIndex searchIndex,
            IDbContextFactory<TvContext> dbContextFactory,
            ILogger<MatchTraktListItemsHandler> logger,
            IEntityLocker entityLocker) : base(traktApiClient, searchRepository, searchIndex, logger)
        {
            _dbContextFactory = dbContextFactory;
            _entityLocker = entityLocker;
        }

        public async Task<Either<BaseError, Unit>> Handle(
            MatchTraktListItems request,
            CancellationToken cancellationToken)
        {
            try
            {
                await using TvContext dbContext = _dbContextFactory.CreateDbContext();

                Validation<BaseError, TraktList> validation = await TraktListMustExist(dbContext, request.TraktListId);
                return await validation.Match(
                    async l => await MatchListItems(dbContext, l).MapT(_ => Unit.Default),
                    error => Task.FromResult<Either<BaseError, Unit>>(error.Join()));
            }
            finally
            {
                if (request.Unlock)
                {
                    _entityLocker.UnlockTrakt();
                }
            }
        }
    }
}
