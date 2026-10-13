using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.Tvdb;
using NzbDrone.Core.MetadataSource.Tvdb.Resource;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MetadataSource.Tvdb
{
    [TestFixture]
    public class TvdbOrderingProjectorFixture : CoreTest
    {
        private static Episode Episode(int tvdbId, int season, int number)
        {
            return new Episode
            {
                TvdbId = tvdbId,
                SeasonNumber = season,
                EpisodeNumber = number,
                Title = $"S{season}E{number}"
            };
        }

        private static TvdbEpisodeResource Listed(int tvdbId, int season, int number, int? absolute = null)
        {
            return new TvdbEpisodeResource
            {
                Id = tvdbId,
                SeasonNumber = season,
                Number = number,
                AbsoluteNumber = absolute
            };
        }

        [Test]
        public void should_renumber_episodes_into_the_alternate_order()
        {
            var a = Episode(1, 1, 1);
            var b = Episode(2, 1, 2);
            var c = Episode(3, 2, 1);

            var result = TvdbOrderingProjector.Project(
                TvdbOrderingIds.Alternate,
                new[] { Listed(3, 1, 1), Listed(1, 1, 2), Listed(2, 1, 3) },
                new List<Episode> { a, b, c });

            a.SeasonNumber.Should().Be(1);
            a.EpisodeNumber.Should().Be(2);
            b.SeasonNumber.Should().Be(1);
            b.EpisodeNumber.Should().Be(3);
            c.SeasonNumber.Should().Be(1);
            c.EpisodeNumber.Should().Be(1);

            // The caller's order is preserved, so renumbering does not reshuffle the list.
            result.Should().Equal(a, b, c);
        }

        [Test]
        public void should_number_an_absolute_order_as_one_run_in_season_one()
        {
            var a = Episode(1, 1, 1);
            var b = Episode(2, 2, 1);
            var c = Episode(3, 2, 2);

            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Absolute,
                new[] { Listed(1, 1, 1, 1), Listed(2, 1, 2, 2), Listed(3, 1, 3, 3) },
                new List<Episode> { a, b, c });

            new[] { a, b, c }.Should().OnlyContain(e => e.SeasonNumber == 1);
            new[] { a, b, c }.Select(e => e.EpisodeNumber).Should().Equal(1, 2, 3);
            new[] { a, b, c }.Select(e => e.AbsoluteEpisodeNumber).Should().Equal(1, 2, 3);
        }

        [Test]
        public void should_use_the_listed_number_when_an_absolute_order_has_no_absolute_number()
        {
            var a = Episode(1, 3, 2);

            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Absolute,
                new[] { Listed(1, 1, 5, null) },
                new List<Episode> { a });

            a.SeasonNumber.Should().Be(1);
            a.EpisodeNumber.Should().Be(5);
            a.AbsoluteEpisodeNumber.Should().Be(5);
        }

        [Test]
        public void should_keep_an_unlisted_episode_that_does_not_collide()
        {
            var a = Episode(1, 1, 1);
            var special = Episode(2, 0, 1);

            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Alternate,
                new[] { Listed(1, 1, 1) },
                new List<Episode> { a, special });

            special.SeasonNumber.Should().Be(0);
            special.EpisodeNumber.Should().Be(1);
        }

        [Test]
        public void should_move_a_colliding_unlisted_episode_to_the_end_of_season_zero()
        {
            var a = Episode(1, 1, 1);
            var b = Episode(2, 1, 2);
            var special = Episode(3, 0, 1);
            var unlisted = Episode(4, 1, 3);

            // The order puts a into slot 1x3, which the unlisted episode already holds.
            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Alternate,
                new[] { Listed(1, 1, 3), Listed(2, 1, 1) },
                new List<Episode> { a, b, special, unlisted });

            a.SeasonNumber.Should().Be(1);
            a.EpisodeNumber.Should().Be(3);
            b.SeasonNumber.Should().Be(1);
            b.EpisodeNumber.Should().Be(1);

            // A real special is never displaced by the move.
            special.SeasonNumber.Should().Be(0);
            special.EpisodeNumber.Should().Be(1);

            unlisted.SeasonNumber.Should().Be(0);
            unlisted.EpisodeNumber.Should().Be(2);
        }

        [Test]
        public void should_never_leave_two_episodes_in_the_same_slot()
        {
            var episodes = new List<Episode>
            {
                Episode(1, 1, 1),
                Episode(2, 1, 2),
                Episode(3, 1, 3),
                Episode(4, 2, 1),
                Episode(5, 0, 1)
            };

            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Alternate,
                new[] { Listed(4, 1, 1), Listed(1, 1, 2) },
                episodes);

            episodes.Select(e => (e.SeasonNumber, e.EpisodeNumber)).Should().OnlyHaveUniqueItems();
        }

        [Test]
        public void should_claim_an_episode_listed_twice_only_once()
        {
            var a = Episode(1, 1, 1);
            var b = Episode(2, 1, 2);

            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Alternate,
                new[] { Listed(1, 1, 1), Listed(1, 1, 5) },
                new List<Episode> { a, b });

            a.SeasonNumber.Should().Be(1);
            a.EpisodeNumber.Should().Be(1);
            b.SeasonNumber.Should().Be(1);
            b.EpisodeNumber.Should().Be(2);
        }

        [Test]
        public void should_not_give_two_listed_episodes_the_same_slot()
        {
            var a = Episode(1, 1, 1);
            var b = Episode(2, 1, 2);

            // Both are listed for 1x1; the first keeps it and the second keeps what it had.
            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Alternate,
                new[] { Listed(1, 1, 1), Listed(2, 1, 1) },
                new List<Episode> { a, b });

            a.EpisodeNumber.Should().Be(1);
            b.SeasonNumber.Should().Be(1);
            b.EpisodeNumber.Should().Be(2);
        }

        [Test]
        public void should_leave_an_episode_alone_when_it_is_listed_without_a_usable_number()
        {
            var a = Episode(1, 2, 4);

            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Alternate,
                new[] { Listed(1, 1, 0) },
                new List<Episode> { a });

            a.SeasonNumber.Should().Be(2);
            a.EpisodeNumber.Should().Be(4);
        }

        [Test]
        public void should_not_match_episodes_without_a_tvdb_id()
        {
            var a = Episode(0, 1, 1);

            TvdbOrderingProjector.Project(
                TvdbOrderingIds.Alternate,
                new[] { Listed(0, 5, 5) },
                new List<Episode> { a });

            a.SeasonNumber.Should().Be(1);
            a.EpisodeNumber.Should().Be(1);
        }

        [Test]
        public void should_keep_every_episode_when_the_ordering_lists_nothing()
        {
            var episodes = new List<Episode> { Episode(1, 1, 1), Episode(2, 1, 2) };

            var result = TvdbOrderingProjector.Project(TvdbOrderingIds.Alternate, new List<TvdbEpisodeResource>(), episodes);

            result.Should().HaveCount(2);
            result.Select(e => e.EpisodeNumber).Should().Equal(1, 2);
        }
    }
}
