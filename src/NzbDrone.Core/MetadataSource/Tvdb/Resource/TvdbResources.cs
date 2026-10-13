using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Tvdb.Resource
{
    // The HTTP client deserializes with Newtonsoft, so every field is named explicitly with
    // JsonProperty. See AGENTS.md: System.Text.Json attributes are ignored without an error.

    /// <summary>
    /// Response of POST /login.
    /// </summary>
    public class TvdbLoginResponse
    {
        [JsonProperty("data")]
        public TvdbLoginData Data { get; set; }
    }

    public class TvdbLoginData
    {
        [JsonProperty("token")]
        public string Token { get; set; }
    }

    /// <summary>
    /// Response of GET /series/{id}/episodes/{season-type}, one page at a time.
    /// </summary>
    public class TvdbEpisodesResponse
    {
        [JsonProperty("data")]
        public TvdbEpisodesData Data { get; set; }
    }

    public class TvdbEpisodesData
    {
        [JsonProperty("episodes")]
        public List<TvdbEpisodeResource> Episodes { get; set; }
    }

    public class TvdbEpisodeResource
    {
        /// <summary>
        /// TheTVDB's episode id. This is the same id SkyHook reports as the episode's TvdbId,
        /// which is what lets an ordering be laid over the episodes already fetched.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("seasonNumber")]
        public int SeasonNumber { get; set; }

        [JsonProperty("number")]
        public int Number { get; set; }

        [JsonProperty("absoluteNumber")]
        public int? AbsoluteNumber { get; set; }
    }
}
