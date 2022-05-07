namespace FSClient.Shared.Test.Models
{
    using System;
    using System.Collections.Specialized;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture]
    public class TrackCollectionTest
    {
        [Test]
        public async Task Should_Add_Track_To_Collection_With_Notify()
        {
            var cts = new TaskCompletionSource<NotifyCollectionChangedEventArgs>();

            var collection = new TrackCollection<AudioTrack>();
            var track = new AudioTrack(null) { Index = -1 };
            collection.CollectionChanged += (s, a) => cts.SetResult(a);

            collection.Add(track);

            var args = await cts.Task;

            Assert.That(args.NewItems, Is.Not.Null);
            Assert.That(args.NewItems![0], Is.EqualTo(track));
            Assert.That(track.Index, Is.EqualTo(0));
            Assert.That(args.NewStartingIndex, Is.EqualTo(0));
        }

        [Test]
        public async Task Should_AddRange_Track_To_Collection_With_Notify()
        {
            var cts = new TaskCompletionSource<NotifyCollectionChangedEventArgs>();

            var collection = new TrackCollection<AudioTrack>();
            var track1 = new AudioTrack(LocalizationHelper.RuLang) { Index = -1 };
            var track2 = new AudioTrack(LocalizationHelper.EnLang) { Index = -1 };
            collection.CollectionChanged += (s, a) => cts.SetResult(a);

            collection.AddRange(new[] { track1, track2 });

            var args = await cts.Task;

            Assert.That(args.NewItems, Is.Not.Null);
            Assert.That(args.NewItems![0], Is.EqualTo(track1));
            Assert.That(args.NewItems[1], Is.EqualTo(track2));
            Assert.That(track1.Index, Is.EqualTo(0));
            Assert.That(track2.Index, Is.EqualTo(1));
            Assert.That(args.NewStartingIndex, Is.EqualTo(0));
        }

        [Test]
        public void Should_AddRange_Track_To_Collection_With_Predefined_Index()
        {
            var collection = new TrackCollection<AudioTrack>();
            var track1 = new AudioTrack(LocalizationHelper.RuLang) { Index = -1 };
            var track2 = new AudioTrack(LocalizationHelper.EnLang) { Index = 6 };

            collection.AddRange(new[] { track1, track2 });

            Assert.That(track1.Index, Is.EqualTo(0));
            Assert.That(track2.Index, Is.EqualTo(6));
        }

        [Test]
        public void Should_AddRange_Track_To_Collection_With_Predefined_Index_And_Overlap()
        {
            var collection = new TrackCollection<AudioTrack>();
            var track1 = new AudioTrack(LocalizationHelper.RuLang) { Index = -1 };
            var track2 = new AudioTrack(LocalizationHelper.EnLang) { Index = 0 };

            collection.AddRange(new[] { track1, track2 });

            Assert.That(track1.Index, Is.EqualTo(1));
            Assert.That(track2.Index, Is.EqualTo(0));
        }

        [Test]
        public void Should_Throw_InvalidOperationException_On_Key_Duplicate()
        {
            var collection = new TrackCollection<AudioTrack>();
            var track1 = new AudioTrack(LocalizationHelper.RuLang) { Index = -1 };
            var track2 = new AudioTrack(LocalizationHelper.EnLang) { Index = 0 };

            collection.Add(track1);
            Assert.That(track1.Index, Is.EqualTo(0));

            Assert.Throws<InvalidOperationException>(() => collection.Add(track2));
        }

        [Test]
        public void Should_Throw_InvalidOperationException_On_Key_Duplicate_Range()
        {
            var collection = new TrackCollection<AudioTrack>();
            var track1 = new AudioTrack(LocalizationHelper.RuLang) { Index = -1 };
            var track2 = new AudioTrack(LocalizationHelper.EnLang) { Index = 2 };
            var track3 = new AudioTrack(LocalizationHelper.UaLang) { Index = 0 };

            collection.Add(track1);
            Assert.That(track1.Index, Is.EqualTo(0));

            Assert.Throws<InvalidOperationException>(() => collection.AddRange(new[] { track2, track2 }));
        }

        [Test]
        public void Should_Throw_InvalidOperationException_On_Key_Duplicate_Input_Range()
        {
            var collection = new TrackCollection<AudioTrack>();
            var track1 = new AudioTrack(LocalizationHelper.RuLang) { Index = 0 };
            var track2 = new AudioTrack(LocalizationHelper.EnLang) { Index = 0 };

            Assert.Throws<InvalidOperationException>(() => collection.AddRange(new[] { track1, track1 }));
        }

        [Test]
        public void Should_Set_Title_With_Required_Info()
        {
            var collection = new TrackCollection<AudioTrack>();
            var track1 = new AudioTrack(LocalizationHelper.RuLang) { Index = -1, Title = null };

            collection.Add(track1);

            Assert.That(track1.Title, Is.Not.Null);
            Assert.That(track1.Title!.Contains((track1.Index + 1).ToString()), Is.True, "Title doesn't contain index");
            if (track1.Language != null)
            {
                Assert.That(track1.Title!.Contains(track1.Language), Is.True, "Title doesn't contain language");
            }
        }
    }
}
