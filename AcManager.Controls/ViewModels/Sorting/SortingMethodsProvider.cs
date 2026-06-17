using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.ViewModels.Sorting {
    public class SortingMethodsProvider<T> where T : AcObjectNew {
        internal sealed class SortingDesc : Displayable, IWithId {
            public SortingDesc(string id, string displayName, bool withGrouping, Func<AcObjectSorter<T>> factory) {
                Id = id;
                WithGrouping = withGrouping;
                Factory = factory;
                DisplayName = displayName;
            }

            public string Id { get; }

            public bool WithGrouping { get; }

            public Func<AcObjectSorter<T>> Factory { get; }
        }

        private class SortingGeneric : AcObjectSorter<T> {
            private readonly string _propName;
            private readonly Func<T, T, int> _comparer;
            private readonly Func<T, string> _groupNameFactory;

            public SortingGeneric(bool usePadding,
                    string propName, Func<T, T, int> comparer, Func<T, string> groupNameFactory) : base(usePadding) {
                _propName = propName;
                _comparer = comparer;
                _groupNameFactory = groupNameFactory;
            }

            public override int Compare(T x, T y) {
                return _comparer(x, y);
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == _propName;
            }

            public override void OnSelected(bool active, bool allowGrouping, AcListPageViewModel<T> parent) {
                if (active) {
                    parent.GroupBy(allowGrouping && _groupNameFactory != null ? _propName : null, _groupNameFactory);
                }
            }
        }

        internal static SortingDesc BuildSortingGen(string name, bool usePadding,
                string propName, Func<T, T, int> comparer, Func<T, string> groupNameFactory) {
            return new SortingDesc($@"g.{propName}", name, groupNameFactory != null, () => new SortingGeneric(usePadding, propName, comparer, groupNameFactory));
        }

        internal static SortingDesc BuildSortingNum(string name, bool usePadding,
                string propName, Func<T, double> factor /* ascending order */, Func<T, string> groupNameFactory) {
            return BuildSortingGen(name, usePadding, propName, (a, b) => (factor(a) - factor(b)).Sign(), groupNameFactory);
        }

        internal static SortingDesc BuildSortingStr(string name, bool usePadding,
                string propName, Func<T, string> factor /* ascending order */, Func<T, string> groupNameFactory) {
            return BuildSortingGen(name, usePadding, propName, (a, b) => string.Compare(factor(a) ?? string.Empty, factor(b) ?? string.Empty, StringComparison.OrdinalIgnoreCase), groupNameFactory);
        }

        private static IEnumerable<SortingDesc> GetAdditionalSortingTypes() {
            if (typeof(T) == typeof(CarObject)) {
                return CarsListSortingMethodsProvider.GetAdditionalSortingTypes().Select(x => x as SortingDesc).NonNull();
            }
            if (typeof(T) == typeof(TrackObject)) {
                return TracksListSortingMethodsProvider.GetAdditionalSortingTypes().Select(x => x as SortingDesc).NonNull();
            }
            return null;
        }

        internal static IEnumerable<SortingDesc> GetSortingTypes() {
            yield return new SortingDesc(string.Empty, "Name", false, null);
            yield return BuildSortingGen("Age", false, nameof(AcObjectNew.Age),
                    (a, b) => (b.CreationDateTime - a.CreationDateTime).TotalDays.Sign(), null);
            var additional = GetAdditionalSortingTypes();
            if (additional != null) {
                yield return null;
                foreach (var type in additional) {
                    yield return type;
                }
                yield return null;
            }
            yield return BuildSortingNum("Rating", false, nameof(AcObjectNew.Rating), a => -(a.Rating ?? -1d), null);
            yield return BuildSortingNum("Favorites first", true, nameof(AcObjectNew.IsFavourite), a => a.IsFavourite ? 0 : 1, null);
            if (typeof(T).IsSubclassOf(typeof(AcJsonObjectNew))) {
                yield return BuildSortingStr("Author", false, nameof(AcJsonObjectNew.Author), a => (a as AcJsonObjectNew)?.Author, a => (a as AcJsonObjectNew)?.Author);
            }
        }

        internal static AcObjectSorter<T> CreateByKey(string key, out bool withGrouping) {
            var pieces = key?.Split('/');
            withGrouping = pieces?.ElementAtOrDefault(1) == @"g";
            return pieces == null ? null : GetSortingTypes().NonNull().GetByIdOrDefault(pieces[0])?.Factory?.Invoke();
        }
    }
}