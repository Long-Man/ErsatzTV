﻿using ErsatzTV.Core;
using ErsatzTV.Core.Domain;
using LanguageExt;
using MediatR;

namespace ErsatzTV.Application.ProgramSchedules.Commands
{
    public record CreateProgramSchedule(
        string Name,
        bool KeepMultiPartEpisodesTogether,
        bool TreatCollectionsAsShows) : IRequest<Either<BaseError, CreateProgramScheduleResult>>;
}
