using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public interface IAcListPageViewModel {
        string GetNumberString(int count);
        string Status { get; }
        void SetCurrentItem(string id);
        AcWrapperCollectionView GetAcWrapperCollectionView();
        IAcManagerNew Manager { get; }
        string SortingBy { get; }
        ContextMenu BuildListContextMenu(Action selectMultiple);
    }

    public abstract class AcListPageViewModel<T> : AcObjectListCollectionViewWrapper<T>, IAcListPageViewModel where T : AcObjectNew {
        private const string KeyBase = "Content";

        public IAcManagerNew Manager { get; }

        protected AcListPageViewModel([NotNull] IAcManagerNew list, IFilter<T> listFilter) : base(list, listFilter, KeyBase, false) {
            Manager = list;
            var sortingBy = ValuesStorage.Get<string>($"{Key}.s");
            if (sortingBy != null) {
                var created = GetSortingImpl(sortingBy);
                if (created != null) {
                    _sortingBy = sortingBy;
                    Sorting = created;
                }
            }
            if (_sortingBy == null) {
                _sortingBy = string.Empty;
            }
        }

        protected sealed class SortingDesc : Displayable, IWithId {
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

        protected virtual IEnumerable<SortingDesc> GetAdditionalSortingTypes() {
            return null;
        }

        protected SortingDesc BuildSortingGen(string name, bool usePadding, 
                string propName, Func<T, T, int> comparer, Func<T, string> groupNameFactory) {
            return new SortingDesc($@"g.{propName}", name, groupNameFactory != null, () => new SortingGeneric(usePadding, propName, comparer, groupNameFactory));
        }

        protected SortingDesc BuildSortingNum(string name, bool usePadding, 
                string propName, Func<T, double> factor /* ascending order */, Func<T, string> groupNameFactory) {
            return BuildSortingGen(name,  usePadding, propName, (a, b) => (factor(a) - factor(b)).Sign(), groupNameFactory);
        }

        protected SortingDesc BuildSortingStr(string name, bool usePadding, 
                string propName, Func<T, string> factor /* ascending order */, Func<T, string> groupNameFactory) {
            return BuildSortingGen(name,  usePadding, propName, (a, b) => string.Compare(factor(a) ?? string.Empty, factor(b) ?? string.Empty, StringComparison.OrdinalIgnoreCase), groupNameFactory);
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
        
        protected IEnumerable<SortingDesc> GetSortingTypes() {
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

        private AcObjectSorter<T> _curSortingImpl;

        protected AcObjectSorter<T> GetSortingImpl(string key) {
            if (_curSortingImpl != null) {
                _curSortingImpl.OnSelected(false, false, this);
                _curSortingImpl = null;
            }
            var pieces = key?.Split('/');
            var created = pieces == null ? null : GetSortingTypes().NonNull().GetByIdOrDefault(pieces[0])?.Factory?.Invoke();
            if (created != null) {
                _curSortingImpl = created;
                _curSortingImpl.OnSelected(true, pieces.ElementAtOrDefault(1) == @"g", this);
                UsePaddingForChildObjects = _curSortingImpl.UsePaddingForChildObjects();
                return _curSortingImpl;
            }
            ResetGroping();
            UsePaddingForChildObjects = true;
            return null;
        }

        public ContextMenu BuildListContextMenu(Action selectMultiple) {
            var copyIDs = new DelegateCommand(() => ClipboardHelper.SetText(MainList.OfType<AcItemWrapper>().Select(x => x.Id).JoinToString('\n')));
            var copyTags = new DelegateCommand(() => ClipboardHelper.SetText(MainList.OfType<AcItemWrapper>().Select(x => x.Value)
                    .OfType<AcJsonObjectNew>().SelectMany(x => x.Tags).OrderBy(x => x).Distinct().JoinToString('\n')));
            var setSorting = new DelegateCommand<string>(mode => {
                SortingBy = mode;
                Sorting = GetSortingImpl(mode);
                ValuesStorage.Set($"{Key}.s", mode);
            });

            MenuItem BuildSortingItem(string displaySort, string sortArg, bool withGrouping) {
                var ret = new MenuItem { Header = displaySort, Command = setSorting, CommandParameter = withGrouping ? $"{sortArg}/g" : sortArg, };
                ret.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(SortingBy)) {
                    Source = this,
                    Converter = EnumToBooleanConverter.Instance,
                    ConverterParameter = sortArg
                });
                return ret;
            }

            var sortMenu = new MenuItem { Header = "Sort by…" };
            foreach (var kv in GetSortingTypes()) {
                if (kv == null) {
                    sortMenu.Items.Add(new Separator());
                } else {
                    if (kv.WithGrouping) {
                        sortMenu.Items.Add(new MenuItem {
                            Header = kv.DisplayName,
                            Items = {
                                BuildSortingItem("Without grouping", kv.Id, false),
                                BuildSortingItem("With grouping", kv.Id, true),
                            }
                        });
                    } else {
                        sortMenu.Items.Add(BuildSortingItem(kv.DisplayName, kv.Id, false));
                    }
                }
            }
            
            var menu = new ContextMenu();
            menu.Items.Add(sortMenu);
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Copy IDs", Command = copyIDs });
            menu.Items.Add(new MenuItem { Header = "Copy tags", Command = copyTags });
            if (selectMultiple != null) {
                menu.Items.Add(new Separator());
                menu.Items.Add(new MenuItem {
                    Header = "Select multiple…",
                    Command = new DelegateCommand(selectMultiple),
                    ToolTip = "Alternatively, you can hold Ctrl or Shift and click a second item in the list"
                });
            }

            return menu;
        }

        protected override void FilteredNumberChanged(int oldValue, int newValue) {
            base.FilteredNumberChanged(oldValue, newValue);
            OnPropertyChanged(nameof(Status));
        }

        protected abstract string GetSubject();

        public string GetNumberString(int count) {
            return PluralizingConverter.PluralizeExt(count, GetSubject());
        }

        public string Status => GetNumberString(MainList.Count);

        private string _sortingBy;

        public string SortingBy {
            get => _sortingBy;
            set => Apply(value, ref _sortingBy);
        }

        public void SetCurrentItem(string id) {
            var found = MainList.OfType<AcItemWrapper>().GetByIdOrDefault(id) ?? MainList.OfType<AcItemWrapper>().FirstOrDefault();
            MainList.MoveCurrentTo(found);
        }

        public AcWrapperCollectionView GetAcWrapperCollectionView() {
            return MainList;
        }

        public static void OnLinkChanged(LinkChangedEventArgs e) {
            LimitedStorage.Move(LimitedSpace.SelectedEntry, GetKey(KeyBase, e.OldValue), GetKey(KeyBase, e.NewValue));
        }
    }
}