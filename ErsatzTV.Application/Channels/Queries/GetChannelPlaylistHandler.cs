﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Interfaces.Repositories;
using ErsatzTV.Core.Iptv;
using LanguageExt;
using MediatR;

namespace ErsatzTV.Application.Channels.Queries
{
    public class GetChannelPlaylistHandler : IRequestHandler<GetChannelPlaylist, ChannelPlaylist>
    {
        private readonly IChannelRepository _channelRepository;

        public GetChannelPlaylistHandler(IChannelRepository channelRepository) =>
            _channelRepository = channelRepository;

        public Task<ChannelPlaylist> Handle(GetChannelPlaylist request, CancellationToken cancellationToken) =>
            _channelRepository.GetAll()
                .Map(channels => EnsureMode(channels, request.Mode))
                .Map(channels => new ChannelPlaylist(request.Scheme, request.Host, channels));

        private static List<Channel> EnsureMode(IEnumerable<Channel> channels, string mode)
        {
            var result = new List<Channel>();
            foreach (Channel channel in channels)
            {
                switch (mode.ToLowerInvariant())
                {
                    case "segmenter":
                        channel.StreamingMode = StreamingMode.HttpLiveStreamingSegmenter;
                        result.Add(channel);
                        break;
                    case "hls-direct":
                        channel.StreamingMode = StreamingMode.HttpLiveStreamingDirect;
                        result.Add(channel);
                        break;
                    case "ts":
                        channel.StreamingMode = StreamingMode.TransportStream;
                        result.Add(channel);
                        break;
                    default:
                        result.Add(channel);
                        break;
                }
            }

            return result;
        }
    }
}
