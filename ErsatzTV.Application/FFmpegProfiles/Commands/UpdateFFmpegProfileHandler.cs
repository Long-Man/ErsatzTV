﻿using System.Threading;
using System.Threading.Tasks;
using ErsatzTV.Core;
using ErsatzTV.Core.Domain;
using ErsatzTV.Infrastructure.Data;
using ErsatzTV.Infrastructure.Extensions;
using LanguageExt;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ErsatzTV.Application.FFmpegProfiles.Commands
{
    public class
        UpdateFFmpegProfileHandler : IRequestHandler<UpdateFFmpegProfile, Either<BaseError, UpdateFFmpegProfileResult>>
    {
        private readonly IDbContextFactory<TvContext> _dbContextFactory;

        public UpdateFFmpegProfileHandler(IDbContextFactory<TvContext> dbContextFactory) =>
            _dbContextFactory = dbContextFactory;

        public async Task<Either<BaseError, UpdateFFmpegProfileResult>> Handle(
            UpdateFFmpegProfile request,
            CancellationToken cancellationToken)
        {
            await using TvContext dbContext = _dbContextFactory.CreateDbContext();
            Validation<BaseError, FFmpegProfile> validation = await Validate(dbContext, request);
            return await validation.Apply(p => ApplyUpdateRequest(dbContext, p, request));
        }

        private async Task<UpdateFFmpegProfileResult> ApplyUpdateRequest(
            TvContext dbContext,
            FFmpegProfile p,
            UpdateFFmpegProfile update)
        {
            p.Name = update.Name;
            p.ThreadCount = update.ThreadCount;
            p.Transcode = update.Transcode;
            p.HardwareAcceleration = update.HardwareAcceleration;
            p.ResolutionId = update.ResolutionId;
            p.NormalizeVideo = update.Transcode && update.NormalizeVideo;
            p.VideoCodec = update.VideoCodec;
            p.VideoBitrate = update.VideoBitrate;
            p.VideoBufferSize = update.VideoBufferSize;
            p.AudioCodec = update.AudioCodec;
            p.AudioBitrate = update.AudioBitrate;
            p.AudioBufferSize = update.AudioBufferSize;
            p.NormalizeLoudness = update.Transcode && update.NormalizeLoudness;
            p.AudioChannels = update.AudioChannels;
            p.AudioSampleRate = update.AudioSampleRate;
            p.NormalizeAudio = update.Transcode && update.NormalizeAudio;
            await dbContext.SaveChangesAsync();
            return new UpdateFFmpegProfileResult(p.Id);
        }

        private static async Task<Validation<BaseError, FFmpegProfile>> Validate(
            TvContext dbContext,
            UpdateFFmpegProfile request) =>
            (await FFmpegProfileMustExist(dbContext, request), ValidateName(request), ValidateThreadCount(request),
                await ResolutionMustExist(dbContext, request))
            .Apply((ffmpegProfileToUpdate, _, _, _) => ffmpegProfileToUpdate);

        private static Task<Validation<BaseError, FFmpegProfile>> FFmpegProfileMustExist(
            TvContext dbContext,
            UpdateFFmpegProfile updateFFmpegProfile) =>
            dbContext.FFmpegProfiles
                .SelectOneAsync(p => p.Id, p => p.Id == updateFFmpegProfile.FFmpegProfileId)
                .Map(o => o.ToValidation<BaseError>("FFmpegProfile does not exist."));

        private static Validation<BaseError, string> ValidateName(UpdateFFmpegProfile updateFFmpegProfile) =>
            updateFFmpegProfile.NotEmpty(x => x.Name)
                .Bind(_ => updateFFmpegProfile.NotLongerThan(50)(x => x.Name));

        private static Validation<BaseError, int> ValidateThreadCount(UpdateFFmpegProfile updateFFmpegProfile) =>
            updateFFmpegProfile.AtLeast(0)(p => p.ThreadCount);

        private static Task<Validation<BaseError, int>> ResolutionMustExist(
            TvContext dbContext,
            UpdateFFmpegProfile updateFFmpegProfile) =>
            dbContext.Resolutions
                .SelectOneAsync(r => r.Id, r => r.Id == updateFFmpegProfile.ResolutionId)
                .MapT(r => r.Id)
                .Map(o => o.ToValidation<BaseError>($"[Resolution] {updateFFmpegProfile.ResolutionId} does not exist"));
    }
}
