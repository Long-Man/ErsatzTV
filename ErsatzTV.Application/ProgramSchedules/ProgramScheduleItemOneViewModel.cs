﻿using System;
using ErsatzTV.Application.MediaCollections;
using ErsatzTV.Application.MediaItems;
using ErsatzTV.Core.Domain;

namespace ErsatzTV.Application.ProgramSchedules
{
    public record ProgramScheduleItemOneViewModel : ProgramScheduleItemViewModel
    {
        public ProgramScheduleItemOneViewModel(
            int id,
            int index,
            StartType startType,
            TimeSpan? startTime,
            ProgramScheduleItemCollectionType collectionType,
            MediaCollectionViewModel collection,
            MultiCollectionViewModel multiCollection,
            SmartCollectionViewModel smartCollection,
            NamedMediaItemViewModel mediaItem,
            PlaybackOrder playbackOrder,
            string customTitle) : base(
            id,
            index,
            startType,
            startTime,
            PlayoutMode.One,
            collectionType,
            collection,
            multiCollection,
            smartCollection,
            mediaItem,
            playbackOrder,
            customTitle)
        {
        }
    }
}
