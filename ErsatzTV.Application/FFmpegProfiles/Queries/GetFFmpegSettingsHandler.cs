﻿using System.Threading;
using System.Threading.Tasks;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.FFmpeg;
using ErsatzTV.Core.Interfaces.Repositories;
using LanguageExt;
using MediatR;

namespace ErsatzTV.Application.FFmpegProfiles.Queries
{
    public class GetFFmpegSettingsHandler : IRequestHandler<GetFFmpegSettings, FFmpegSettingsViewModel>
    {
        private readonly IConfigElementRepository _configElementRepository;

        public GetFFmpegSettingsHandler(IConfigElementRepository configElementRepository) =>
            _configElementRepository = configElementRepository;

        public async Task<FFmpegSettingsViewModel> Handle(
            GetFFmpegSettings request,
            CancellationToken cancellationToken)
        {
            Option<string> ffmpegPath = await _configElementRepository.GetValue<string>(ConfigElementKey.FFmpegPath);
            Option<string> ffprobePath = await _configElementRepository.GetValue<string>(ConfigElementKey.FFprobePath);
            Option<int> defaultFFmpegProfileId =
                await _configElementRepository.GetValue<int>(ConfigElementKey.FFmpegDefaultProfileId);
            Option<bool> saveReports =
                await _configElementRepository.GetValue<bool>(ConfigElementKey.FFmpegSaveReports);
            Option<string> preferredLanguageCode =
                await _configElementRepository.GetValue<string>(ConfigElementKey.FFmpegPreferredLanguageCode);
            Option<int> watermark =
                await _configElementRepository.GetValue<int>(ConfigElementKey.FFmpegGlobalWatermarkId);
            Option<int> vaapiDriver =
                await _configElementRepository.GetValue<int>(ConfigElementKey.FFmpegVaapiDriver);

            var result = new FFmpegSettingsViewModel
            {
                FFmpegPath = await ffmpegPath.IfNoneAsync(string.Empty),
                FFprobePath = await ffprobePath.IfNoneAsync(string.Empty),
                DefaultFFmpegProfileId = await defaultFFmpegProfileId.IfNoneAsync(0),
                SaveReports = await saveReports.IfNoneAsync(false),
                PreferredLanguageCode = await preferredLanguageCode.IfNoneAsync("eng"),
                VaapiDriver = (VaapiDriver)await vaapiDriver.IfNoneAsync(0)
            };

            foreach (int watermarkId in watermark)
            {
                result.GlobalWatermarkId = watermarkId;
            }

            return result;
        }
    }
}
