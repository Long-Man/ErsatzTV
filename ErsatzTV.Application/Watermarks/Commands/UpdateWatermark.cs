﻿using ErsatzTV.Core;
using ErsatzTV.Core.Domain;
using LanguageExt;
using MediatR;

namespace ErsatzTV.Application.Watermarks.Commands
{
    public record UpdateWatermark(
        int Id,
        string Name,
        string Image,
        ChannelWatermarkMode Mode,
        ChannelWatermarkImageSource ImageSource,
        ChannelWatermarkLocation Location,
        ChannelWatermarkSize Size,
        int Width,
        int HorizontalMargin,
        int VerticalMargin,
        int FrequencyMinutes,
        int DurationSeconds,
        int Opacity) : IRequest<Either<BaseError, UpdateWatermarkResult>>;

    public record UpdateWatermarkResult(int WatermarkId) : EntityIdResult(WatermarkId);
}
