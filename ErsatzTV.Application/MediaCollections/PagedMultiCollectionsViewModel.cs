﻿using System.Collections.Generic;

namespace ErsatzTV.Application.MediaCollections
{
    public record PagedMultiCollectionsViewModel(int TotalCount, List<MultiCollectionViewModel> Page);
}
