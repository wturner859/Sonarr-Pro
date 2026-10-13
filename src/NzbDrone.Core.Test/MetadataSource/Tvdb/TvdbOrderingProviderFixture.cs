using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Tvdb;
using NzbDrone.Core.MetadataSource.Tvdb.Resource;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MetadataSource.Tvdb
{
    [TestFixture]
    public class TvdbOrderingProviderFixture : CoreTest<TvdbOrderingProvider>
    {
        private const string LoginJson = "{\"data\":{\"token\":\"test-token\"},\"status\":\"success\"}";

        private const string AbsoluteJson = "{\"data\":{\"episodes\":[" +
            "{\"id\":11,\"seasonNumber\":1,\"number\":1,\"absoluteNumber\":1}," +
            "{\"id\":12,\"seasonNumber\":1,\"number\":2,\"absoluteNumber\":2}," +
            "{\"id\":21,\"seasonNumber\":2,\"number\":1,\"absoluteNumber\":3}]},\"status\":\"success\"}";

        private const string AlternateJson = "{\"data\":{\"episodes\":[" +
            "{\"id\":21,\"seasonNumber\":1,\"number\":1}," +
            "{\"id\":11,\"seasonNumber\":1,\"number\":2}]},\"status\":\"success\"}";

        private const string EmptyJson = "{\"data\":{\"episodes\":[]},\"status\":\"success\"}";

        private void GivenApiKey(string key = "api-key")
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.TvdbApiKey)
                  .Returns(key);

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.TvdbPin)
                  .Returns(string.Empty);
        }

        private void GivenLoginSucceeds()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Post<TvdbLoginResponse>(It.IsAny<HttpRequest>()))
                  .Returns((HttpRequest r) => new HttpResponse<TvdbLoginResponse>(new HttpResponse(r, new HttpHeader(), LoginJson)));
        }

        /// <summary>
        /// Serves a season type's first page, and an empty page after it, as TheTVDB does
        /// when a series has run out of episodes.
        /// </summary>
        private void GivenSeasonType(string seasonType, string firstPageJson, HttpStatusCode status = HttpStatusCode.OK)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<TvdbEpisodesResponse>(It.Is<HttpRequest>(r => r.Url.FullUri.Contains($"/episodes/{seasonType}"))))
                  .Returns((HttpRequest r) =>
                  {
                      var json = r.Url.FullUri.Contains("page=0") ? firstPageJson : EmptyJson;

                      return new HttpResponse<TvdbEpisodesResponse>(new HttpResponse(r, new HttpHeader(), json, status));
                  });
        }

        private static Episode Episode(int tvdbId, int season, int number)
        {
            return new Episode { TvdbId = tvdbId, SeasonNumber = season, EpisodeNumber = number };
        }

        [Test]
        public void should_offer_nothing_without_an_api_key()
        {
            GivenApiKey(string.Empty);

            Subject.GetOrderings(81189).Should().BeEmpty();

            Mocker.GetMock<IHttpClient>().Verify(v => v.Post<TvdbLoginResponse>(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public void should_offer_nothing_for_a_placeholder_series_id()
        {
            GivenApiKey();

            Subject.GetOrderings(-5).Should().BeEmpty();
            Subject.GetOrderings(0).Should().BeEmpty();
        }

        [Test]
        public void should_offer_the_orders_thetvdb_lists_after_the_default()
        {
            GivenApiKey();
            GivenLoginSucceeds();
            GivenSeasonType("absolute", AbsoluteJson);
            GivenSeasonType("alternate", AlternateJson);

            var orderings = Subject.GetOrderings(81189);

            orderings.Select(o => o.Id).Should().Equal(null, TvdbOrderingIds.Absolute, TvdbOrderingIds.Alternate);

            var absolute = orderings.Single(o => o.Id == TvdbOrderingIds.Absolute);
            absolute.IsAbsolute.Should().BeTrue();
            absolute.EpisodeCount.Should().Be(3);
            absolute.SeasonCount.Should().Be(1);

            var alternate = orderings.Single(o => o.Id == TvdbOrderingIds.Alternate);
            alternate.IsAbsolute.Should().BeFalse();
            alternate.EpisodeCount.Should().Be(2);
        }

        [Test]
        public void should_leave_out_an_order_thetvdb_does_not_have()
        {
            GivenApiKey();
            GivenLoginSucceeds();
            GivenSeasonType("absolute", string.Empty, HttpStatusCode.NotFound);
            GivenSeasonType("alternate", AlternateJson);

            Subject.GetOrderings(81189).Select(o => o.Id).Should().Equal(null, TvdbOrderingIds.Alternate);
        }

        [Test]
        public void should_offer_no_choice_when_thetvdb_has_only_the_aired_order()
        {
            GivenApiKey();
            GivenLoginSucceeds();
            GivenSeasonType("absolute", string.Empty, HttpStatusCode.NotFound);
            GivenSeasonType("alternate", string.Empty, HttpStatusCode.NotFound);

            Subject.GetOrderings(81189).Should().BeEmpty();
        }

        [Test]
        public void should_offer_nothing_instead_of_failing_when_the_key_is_rejected()
        {
            GivenApiKey();

            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Post<TvdbLoginResponse>(It.IsAny<HttpRequest>()))
                  .Returns((HttpRequest r) => new HttpResponse<TvdbLoginResponse>(new HttpResponse(r, new HttpHeader(), "{}", HttpStatusCode.Unauthorized)));

            Subject.GetOrderings(81189).Should().BeEmpty();
        }

        [Test]
        public void should_renumber_episodes_into_the_chosen_order()
        {
            GivenApiKey();
            GivenLoginSucceeds();
            GivenSeasonType("alternate", AlternateJson);

            var a = Episode(11, 1, 1);
            var b = Episode(21, 2, 1);

            Subject.ApplyOrdering(TvdbOrderingIds.Alternate, 81189, new List<Episode> { a, b });

            a.EpisodeNumber.Should().Be(2);
            b.EpisodeNumber.Should().Be(1);
        }

        [Test]
        public void should_log_in_again_once_when_the_cached_token_is_refused()
        {
            GivenApiKey();
            GivenLoginSucceeds();

            var request = new HttpRequest("https://api4.thetvdb.com/v4/series/81189/episodes/alternate?page=0");

            Mocker.GetMock<IHttpClient>()
                  .SetupSequence(c => c.Get<TvdbEpisodesResponse>(It.IsAny<HttpRequest>()))
                  .Returns(new HttpResponse<TvdbEpisodesResponse>(new HttpResponse(request, new HttpHeader(), "{}", HttpStatusCode.Unauthorized)))
                  .Returns(new HttpResponse<TvdbEpisodesResponse>(new HttpResponse(request, new HttpHeader(), AlternateJson)))
                  .Returns(new HttpResponse<TvdbEpisodesResponse>(new HttpResponse(request, new HttpHeader(), EmptyJson)));

            var a = Episode(11, 1, 1);

            Subject.ApplyOrdering(TvdbOrderingIds.Alternate, 81189, new List<Episode> { a });

            a.EpisodeNumber.Should().Be(2);

            Mocker.GetMock<IHttpClient>().Verify(v => v.Post<TvdbLoginResponse>(It.IsAny<HttpRequest>()), Times.Exactly(2));
        }

        [Test]
        public void should_not_touch_episodes_for_an_ordering_that_is_not_ours()
        {
            var a = Episode(11, 1, 1);

            var result = Subject.ApplyOrdering("a1b2c3d4", 81189, new List<Episode> { a });

            result.Should().ContainSingle().Which.Should().BeSameAs(a);
            a.EpisodeNumber.Should().Be(1);

            Mocker.GetMock<IHttpClient>().Verify(v => v.Post<TvdbLoginResponse>(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public void should_refuse_to_fall_back_to_aired_order_when_the_ordering_has_gone()
        {
            GivenApiKey();
            GivenLoginSucceeds();
            GivenSeasonType("alternate", string.Empty, HttpStatusCode.NotFound);

            var a = Episode(11, 1, 1);

            Assert.Throws<TvdbOrderingUnavailableException>(() =>
                Subject.ApplyOrdering(TvdbOrderingIds.Alternate, 81189, new List<Episode> { a }));

            a.SeasonNumber.Should().Be(1);
            a.EpisodeNumber.Should().Be(1);
        }

        [Test]
        public void should_say_so_when_applying_an_ordering_without_an_api_key()
        {
            GivenApiKey(string.Empty);

            Assert.Throws<TvdbApiKeyMissingException>(() =>
                Subject.ApplyOrdering(TvdbOrderingIds.Alternate, 81189, new List<Episode> { Episode(11, 1, 1) }));
        }
    }
}
