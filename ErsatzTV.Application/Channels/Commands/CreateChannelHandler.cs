﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ErsatzTV.Core;
using ErsatzTV.Core.Domain;
using ErsatzTV.Infrastructure.Data;
using ErsatzTV.Infrastructure.Extensions;
using LanguageExt;
using MediatR;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace ErsatzTV.Application.Channels.Commands
{
    public class CreateChannelHandler : IRequestHandler<CreateChannel, Either<BaseError, CreateChannelResult>>
    {
        private readonly IDbContextFactory<TvContext> _dbContextFactory;

        public CreateChannelHandler(IDbContextFactory<TvContext> dbContextFactory) => _dbContextFactory = dbContextFactory;

        public async Task<Either<BaseError, CreateChannelResult>> Handle(
            CreateChannel request,
            CancellationToken cancellationToken)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            Validation<BaseError, Channel> validation = await Validate(dbContext, request);
            return await validation.Apply(c => PersistChannel(dbContext, c));
        }

        private static async Task<CreateChannelResult> PersistChannel(TvContext dbContext, Channel channel)
        {
            await dbContext.Channels.AddAsync(channel);
            await dbContext.SaveChangesAsync();
            return new CreateChannelResult(channel.Id);
        }

        private static async Task<Validation<BaseError, Channel>> Validate(TvContext dbContext, CreateChannel request) =>
            (ValidateName(request), await ValidateNumber(dbContext, request),
                await FFmpegProfileMustExist(dbContext, request),
                ValidatePreferredLanguage(request),
                await WatermarkMustExist(dbContext, request))
            .Apply(
                (name, number, ffmpegProfileId, preferredLanguageCode, watermarkId) =>
                {
                    var artwork = new List<Artwork>();
                    if (!string.IsNullOrWhiteSpace(request.Logo))
                    {
                        artwork.Add(
                            new Artwork
                            {
                                Path = request.Logo,
                                ArtworkKind = ArtworkKind.Logo,
                                DateAdded = DateTime.UtcNow,
                                DateUpdated = DateTime.UtcNow
                            });
                    }

                    var channel = new Channel(Guid.NewGuid())
                    {
                        Name = name,
                        Number = number,
                        FFmpegProfileId = ffmpegProfileId,
                        StreamingMode = request.StreamingMode,
                        Artwork = artwork,
                        PreferredLanguageCode = preferredLanguageCode
                    };

                    foreach (int id in watermarkId)
                    {
                        channel.WatermarkId = id;
                    }

                    return channel;
                });

        private static Validation<BaseError, string> ValidateName(CreateChannel createChannel) =>
            createChannel.NotEmpty(c => c.Name)
                .Bind(_ => createChannel.NotLongerThan(50)(c => c.Name));

        private static Validation<BaseError, string> ValidatePreferredLanguage(CreateChannel createChannel) =>
            Optional(createChannel.PreferredLanguageCode ?? string.Empty)
                .Filter(
                    lc => string.IsNullOrWhiteSpace(lc) || CultureInfo.GetCultures(CultureTypes.NeutralCultures).Any(
                        ci => string.Equals(ci.ThreeLetterISOLanguageName, lc, StringComparison.OrdinalIgnoreCase)))
                .ToValidation<BaseError>("Preferred language code is invalid");

        private static async Task<Validation<BaseError, string>> ValidateNumber(TvContext dbContext, CreateChannel createChannel)
        {
            Option<Channel> maybeExistingChannel = await dbContext.Channels
                .SelectOneAsync(c => c.Number, c => c.Number == createChannel.Number);
            return maybeExistingChannel.Match<Validation<BaseError, string>>(
                _ => BaseError.New("Channel number must be unique"),
                () =>
                {
                    if (Regex.IsMatch(createChannel.Number, Channel.NumberValidator))
                    {
                        return createChannel.Number;
                    }

                    return BaseError.New("Invalid channel number; one decimal is allowed for subchannels");
                });
        }

        private static Task<Validation<BaseError, int>> FFmpegProfileMustExist(
            TvContext dbContext,
            CreateChannel createChannel) =>
            dbContext.FFmpegProfiles
                .CountAsync(p => p.Id == createChannel.FFmpegProfileId)
                .Map(Optional)
                .Filter(c => c > 0)
                .MapT(_ => createChannel.FFmpegProfileId)
                .Map(o => o.ToValidation<BaseError>($"FFmpegProfile {createChannel.FFmpegProfileId} does not exist."));

        private static async Task<Validation<BaseError, Option<int>>> WatermarkMustExist(
            TvContext dbContext,
            CreateChannel createChannel)
        {
            if (createChannel.WatermarkId is null)
            {
                return Option<int>.None;
            }

            return await dbContext.ChannelWatermarks
                .CountAsync(w => w.Id == createChannel.WatermarkId)
                .Map(Optional)
                .Filter(c => c > 0)
                .MapT(_ => Optional(createChannel.WatermarkId))
                .Map(o => o.ToValidation<BaseError>($"Watermark {createChannel.WatermarkId} does not exist."));
        }
    }
}
