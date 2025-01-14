﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Extensions
{
    [SuppressMessage("ReSharper", "VSTHRD003")]
    public static class ListToActionResult
    {
        public static Task<IActionResult> ToActionResult<T>(this Task<List<T>> list) =>
            list.Map<List<T>, IActionResult>(l => new OkObjectResult(l));
    }
}
