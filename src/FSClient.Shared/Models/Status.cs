namespace FSClient.Shared.Models
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    public enum StatusType
    {
        Unknown = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_Pilot))]
        Pilot,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_Ongoing))]
        Ongoing,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_Canceled))]
        Canceled,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_Released))]
        Released,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_InProduction))]
        InProduction,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_Anons))]
        Anons,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_PostProduction))]
        PostProduction,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_Rumored))]
        Rumored,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StatusType_Paused))]
        Paused
    }

    public struct Status
    {
        public Status(int? currentSeason = null, int? currentEpisode = null, int? totalEpisodes = null, StatusType type = StatusType.Unknown)
        {
            CurrentSeason = currentSeason;
            CurrentEpisode = currentEpisode;
            TotalEpisodes = totalEpisodes;
            Type = type;
        }

        public int? CurrentSeason { get; }

        public int? CurrentEpisode { get; }
        public int? TotalEpisodes { get; }

        public StatusType Type { get; }

        public override string ToString()
        {
            if (Type >= StatusType.InProduction
                && Type <= StatusType.Rumored)
            {
                return string.Empty;
            }

            var currentEpisode = CurrentEpisode ?? TotalEpisodes;

            if (!currentEpisode.HasValue
                || currentEpisode == 0)
            {
                return string.Empty;
            }

            var isOngoingSymbol = Type == StatusType.Ongoing || Type == StatusType.Pilot ? "+" : "";
            if (CurrentSeason is int currentSeason)
            {
                return $"s{currentSeason}e{currentEpisode}{isOngoingSymbol}";
            }
            else if (TotalEpisodes > 0
                && CurrentEpisode > 0
                && TotalEpisodes != CurrentEpisode
                && Type != StatusType.Released)
            {
                return $"{CurrentEpisode}/{TotalEpisodes}";
            }
            else
            {
                // TODO https://github.com/Humanizr/Humanizer/issues/891
                // return Strings.File_Episode.ToLower().ToQuantity(currentEpisode.Value);
                return $"ep{currentEpisode}{isOngoingSymbol}";
            }
        }
    }
}
