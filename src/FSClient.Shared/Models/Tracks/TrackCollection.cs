namespace FSClient.Shared.Models
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;

    using FSClient.Shared.Mvvm;

    public class TrackCollection<TTrack> : ReadOnlyCollection<TTrack>, INotifyCollectionChanged
        where TTrack : ITrack
    {
        private readonly ContextSafeEvent<NotifyCollectionChangedEventHandler> collectionChangedEvent;

        public TrackCollection() : base(new List<TTrack>())
        {
            collectionChangedEvent = new ContextSafeEvent<NotifyCollectionChangedEventHandler>();
        }

        public TrackCollection(IEnumerable<TTrack> tracks) : base(tracks.ToList())
        {
            collectionChangedEvent = new ContextSafeEvent<NotifyCollectionChangedEventHandler>();
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => collectionChangedEvent.Register(value);
            remove => collectionChangedEvent.Unregister(value);
        }

        public void Add(TTrack track)
        {
            UpdateTrackInfo(track, Items.Count);

            Items.Add(track);

            collectionChangedEvent.Invoke(hanler =>
                hanler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, track, track.Index)));
        }

        public void AddRange(IList<TTrack> tracks)
        {
            var hasInputDuplicate = tracks
                .Select(t => t.Index)
                .Where(t => t >= 0).GroupBy(t => t)
                .Any(g => g.Count() > 1);
            if (hasInputDuplicate)
            {
                throw new InvalidOperationException("Input tracks has duplicate index");
            }

            var list = (List<TTrack>)Items;

            foreach (var track in tracks.OrderBy(t => t.Index >= 0 ? t.Index : int.MaxValue))
            {
                if (track.Index < 0)
                {
                    for (var i = 0; i < list.Capacity + tracks.Count; i++)
                    {
                        if (Items.Concat(tracks).FirstOrDefault(t => t.Index == i) == null)
                        {
                            UpdateTrackInfo(track, i);
                            break;
                        }
                    }
                }
                else
                {
                    UpdateTrackInfo(track, track.Index);
                }
            }

            var startIndex = Items.Count;
            list.AddRange(tracks);

            collectionChangedEvent.Invoke(hanler =>
                hanler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)tracks, startIndex)));
        }

        private void UpdateTrackInfo(ITrack track, int index)
        {
            if (track.Index >= 0
                && Items.FirstOrDefault(i => i.Index == track.Index) != null)
            {
                throw new InvalidOperationException($"Track with index={track.Index} already exist");
            }

            track.Index = index;

            if (string.IsNullOrEmpty(track.Title))
            {
                track.Title = string.IsNullOrEmpty(track.Language)
                    ? $"Track #{track.Index + 1}"
                    : $"{track.Language}: Track #{track.Index + 1}";
            }
        }
    }

    public static class TrackCollection
    {
        public static TrackCollection<TTrack> Empty<TTrack>()
            where TTrack : ITrack
        {
            return new TrackCollection<TTrack>(Array.Empty<TTrack>());
        }
    }
}
