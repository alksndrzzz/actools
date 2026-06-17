using System.Collections.Generic;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Controls.ViewModels.Sorting {
    internal class CarsListSortingMethodsProvider : SortingMethodsProvider<CarObject> {
        internal static IEnumerable<SortingDesc> GetAdditionalSortingTypes() {
            yield return BuildSortingStr("Brand", true, nameof(CarObject.Brand), c => c.Brand, c => c.Brand);
            yield return BuildSortingStr("Country", true, nameof(CarObject.Country), c => c.Country, c => c.Country);
            yield return BuildSortingNum("Year", false, nameof(CarObject.Year), c => c.Year ?? 9999, c => c.Year?.ToInvariantString() ?? "?");
            yield return BuildSortingNum("Last used", false, nameof(CarObject.LastUsedAt), c => -c.LastUsedAt.ToUnixTimestamp(), null);
            yield return BuildSortingNum("Driven distance", false, nameof(CarObject.TotalDrivenDistance), c => -c.TotalDrivenDistance, null);
        }
    }
}