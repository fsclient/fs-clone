namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;

    public class MediaViewModel : ViewModelBase, IStateSaveable
    {
        private readonly IFileManager fileManager;
        private readonly IHistoryManager historyManager;
        private NodeOpenWay lastOpenWay;

        public MediaViewModel(
            IFileManager fileManager,
            IHistoryManager historyManager)
        {
            this.fileManager = fileManager;
            this.historyManager = historyManager;

            currentVideo = fileManager.LastVideo;

            SetPositionForCurrentCommand = new AsyncCommand<float>(
                (p, _) => SetPositionForCurrentAsync(p),
                _ => CurrentFile != null);

            SetAndPreloadFileCommand = new AsyncCommand<File>(
                OpenFileAsync,
                (file) => file != null,
                AsyncCommandConflictBehaviour.WaitPrevious);

            HandleVideoStopedCommand = new AsyncCommand<Video>(
                (video, ct) => fileManager.HandleVideoStopedAsync(video, ct),
                (video) => video != null,
                AsyncCommandConflictBehaviour.WaitPrevious);

            GoNextCommand = new AsyncCommand(
                GoNextAsync,
                () => Playlist.LastOrDefault() != CurrentFile,
                AsyncCommandConflictBehaviour.WaitPrevious);

            GoPreviousCommand = new AsyncCommand(
                GoPreviousAsync,
                () => Playlist.FirstOrDefault() != CurrentFile,
                AsyncCommandConflictBehaviour.WaitPrevious);

            fileManager.VideoOpened += (video, way) =>
            {
                if ((way == NodeOpenWay.InApp
                    || way == NodeOpenWay.InSeparatedWindow)
                    && video != currentVideo)
                {
                    lastOpenWay = way;
                    currentVideo = video;
                    OnPropertyChanged(nameof(Playlist));
                    OnPropertyChanged(nameof(CurrentFile));
                    OnPropertyChanged(nameof(CurrentVideo));

                    SetPositionForCurrentCommand.RaiseCanExecuteChanged();
                    GoNextCommand.RaiseCanExecuteChanged();
                    GoPreviousCommand.RaiseCanExecuteChanged();
                }
            };
        }

        public AsyncCommand<float> SetPositionForCurrentCommand { get; }
        public AsyncCommand<File> SetAndPreloadFileCommand { get; }
        public AsyncCommand<Video> HandleVideoStopedCommand { get; }
        public AsyncCommand GoNextCommand { get; }
        public AsyncCommand GoPreviousCommand { get; }

        public IReadOnlyList<File> Playlist => currentVideo?.ParentFile?.Playlist ?? new List<File>();

        private Video? currentVideo;

        public bool IsFileLoading
        {
            get => Get(false);
            set => Set(value);
        }

        public File? CurrentFile
        {
            get => currentVideo?.ParentFile;
            set
            {
                if (value != null)
                {
                    OpenFile(value);
                }
            }
        }

        public Video? CurrentVideo
        {
            get => currentVideo;
            set
            {
                if (value != null)
                {
                    OpenVideo(value);
                }
            }
        }

        public Uri? SaveStateToUri()
        {
            var currentVideoLink = CurrentFile?.FrameLink ?? CurrentVideo?.SingleLink;
            if (currentVideoLink == null)
            {
                return currentVideoLink;
            }

            return UriParserHelper.GetProtocolUriFromViewModel(NavigationPageType.Video,
                new Dictionary<string, string?>
                {
                    ["link"] = currentVideoLink.ToString()
                });
        }

        private async void OpenFile(File f)
        {
            await OpenFileAsync(f, default).ConfigureAwait(false);
        }

        private async Task<bool> OpenFileAsync(File f, CancellationToken cancellationToken)
        {
            if (f == null)
            {
                return false;
            }

            IsFileLoading = true;

            var result = false;

            var video = currentVideo != null
                ? await f.GetSimilarAsync(currentVideo, cancellationToken).ConfigureAwait(false)
                : await f.GetDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (video != null)
            {
                result = await fileManager.OpenVideoAsync(video, lastOpenWay, cancellationToken).ConfigureAwait(false);
            }

            IsFileLoading = false;

            return result;
        }

        private async void OpenVideo(Video v)
        {
            await fileManager.OpenVideoAsync(v, lastOpenWay, default).ConfigureAwait(false);
        }

        private Task SetPositionForCurrentAsync(float position)
        {
            if (CurrentFile == null)
            {
                return Task.CompletedTask;
            }

            CurrentFile.Position = position;
            return historyManager.UpsertAsync(new[] { CurrentFile });
        }

        private async Task<bool> GoNextAsync(CancellationToken cancellationToken = default)
        {
            var currentVideo = CurrentVideo;
            if (currentVideo?.ParentFile == null)
            {
                return false;
            }

            await fileManager.HandleVideoStopedAsync(currentVideo, cancellationToken).ConfigureAwait(false);

            var ind = Playlist.IndexOf(currentVideo.ParentFile);
            if (ind < -1 || Playlist.Count <= ++ind)
            {
                return false;
            }

            return await OpenFileAsync(Playlist[ind], cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> GoPreviousAsync(CancellationToken cancellationToken = default)
        {
            var currentVideo = CurrentVideo;
            if (currentVideo?.ParentFile == null)
            {
                return false;
            }

            await fileManager.HandleVideoStopedAsync(currentVideo, cancellationToken).ConfigureAwait(false);

            var ind = Playlist.IndexOf(currentVideo.ParentFile);
            if (ind < -1 || --ind < 0)
            {
                return false;
            }

            return await OpenFileAsync(Playlist[ind], cancellationToken).ConfigureAwait(false);
        }
    }
}
