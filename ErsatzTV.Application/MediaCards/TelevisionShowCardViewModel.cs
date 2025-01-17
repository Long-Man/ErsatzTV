﻿namespace ErsatzTV.Application.MediaCards
{
    public record TelevisionShowCardViewModel
        (int TelevisionShowId, string Title, string Subtitle, string SortTitle, string Poster) : MediaCardViewModel(
            TelevisionShowId,
            Title,
            Subtitle,
            SortTitle,
            Poster);
}
