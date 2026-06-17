using System.Collections.Generic;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Controls.ViewModels.Sorting {
    internal class TracksListSortingMethodsProvider : SortingMethodsProvider<TrackObject> {
        internal static IEnumerable<SortingDesc> GetAdditionalSortingTypes() {
            yield return BuildSortingStr("Country", true, nameof(TrackObject.Country), c => c.Country, c=> c.Country);
            yield return BuildSortingNum("Last used", false, nameof(TrackObject.LastUsedAt), c => -c.LastUsedAt.ToUnixTimestamp(), null);
            yield return BuildSortingNum("Driven distance", false, nameof(TrackObject.TotalDrivenDistance), c => -c.TotalDrivenDistance, null);
        }
    }
}