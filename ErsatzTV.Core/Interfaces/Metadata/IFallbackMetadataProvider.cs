﻿using System.Collections.Generic;
using ErsatzTV.Core.Domain;
using LanguageExt;

namespace ErsatzTV.Core.Interfaces.Metadata
{
    public interface IFallbackMetadataProvider
    {
        ShowMetadata GetFallbackMetadataForShow(string showFolder);
        ArtistMetadata GetFallbackMetadataForArtist(string artistFolder);
        List<EpisodeMetadata> GetFallbackMetadata(Episode episode);
        MovieMetadata GetFallbackMetadata(Movie movie);
        Option<MusicVideoMetadata> GetFallbackMetadata(MusicVideo musicVideo);
        string GetSortTitle(string title);
    }
}
