﻿using ErsatzTV.Application.ProgramSchedules.Commands;
using ErsatzTV.Core.Domain;

namespace ErsatzTV.ViewModels
{
    public class ProgramScheduleEditViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool KeepMultiPartEpisodesTogether { get; set; }
        public bool TreatCollectionsAsShows { get; set; }

        public UpdateProgramSchedule ToUpdate() =>
            new(Id, Name, KeepMultiPartEpisodesTogether, TreatCollectionsAsShows);

        public CreateProgramSchedule ToCreate() =>
            new(Name, KeepMultiPartEpisodesTogether, TreatCollectionsAsShows);
    }
}
