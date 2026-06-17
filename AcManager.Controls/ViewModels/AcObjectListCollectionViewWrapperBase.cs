using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using AcManager.Controls.ViewModels.Sorting;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public interface IAcObjectListCollectionViewWrapper {
        bool Load([CanBeNull] AcListPage listPage);
        bool Unload();
    }

    public abstract class AcObjectListCollectionViewWrapperBase<T> : NotifyPropertyChanged, IAcObjectListCollectionViewWrapper, IComparer where T : AcObjectNew {
        [NotNull]
        private readonly IAcManagerNew _manager;

        [NotNull]
        private readonly IAcObjectList _list;

        [CanBeNull]
        private readonly IFilter<T> _listFilter;

        [NotNull]
        private readonly AcWrapperCollectionView _mainList;

        [NotNull]
        public AcWrapperCollectionView MainList {
            get {
                if (!Loaded) {
                    Load(null);
                }
                return _mainList;
            }
        }

        [CanBeNull]
        protected AcItemWrapper CurrentItem => Loaded ? _mainList.CurrentItem as AcItemWrapper : null;

        private readonly bool _allowNonSelected;

        protected AcObjectListCollectionViewWrapperBase([NotNull] IAcManagerNew manager, IFilter<T> listFilter, bool allowNonSelected) {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _list = _manager.WrappersAsIList;
            _mainList = new AcWrapperCollectionView(_list);
            _listFilter = listFilter;
            _allowNonSelected = allowNonSelected;
        }

        private DelegateCommand _addNewCommand;

        public DelegateCommand AddNewCommand => _addNewCommand ?? (_addNewCommand = new DelegateCommand(() => {
            try {
                (_manager as ICreatingManager)?.AddNew();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t add a new object", e);
            }
        }, () => _manager is ICreatingManager, true));

        private bool _collectionReady;

        private void OnCollectionReady(object sender, EventArgs e) {
            if (!Loaded) return;
            _collectionReady = true;
            _mainList.Refresh();
        }

        private bool _grouped;
        private string _groupByPropertyName;
        private GroupDescription _groupDescription;
        private GroupByConverter _groupByConverter;

        [CanBeNull]
        public delegate string GroupByConverter([CanBeNull] string input);

        public void GroupBy([NotNull] string propertyName, [NotNull] GroupByConverter converter) {
            _groupByPropertyName = propertyName;
            _groupByConverter = converter;
            if (Loaded) {
                SetGrouping();
            }
        }

        public void GroupBy([NotNull] string propertyName, [NotNull] GroupDescription description) {
            _groupByPropertyName = propertyName;
            _groupDescription = description;
            if (Loaded) {
                SetGrouping();
            }
        }

        public void GroupBy([CanBeNull] string propertyName, [CanBeNull] Func<T, string> groupNameFactory) {
            _groupByPropertyName = propertyName;
            _groupDescription = propertyName == null ? null : new CallbackGroupDescription(groupNameFactory);
            if (Loaded) {
                SetGrouping();
            }
        }

        private class CallbackGroupDescription : GroupDescription {
            private Func<T, string> _groupNameFactory;
            
            public CallbackGroupDescription(Func<T, string> groupNameFactory) {
                _groupNameFactory = groupNameFactory;
            }

            public override object GroupNameFromItem(object item, int level, CultureInfo culture) {
                string ret = null;
                if ((item as AcItemWrapper)?.Value is T t) ret = _groupNameFactory(t);
                return string.IsNullOrEmpty(ret) ? @"?" : ret;
            }
        }

        public void ResetGroping() {
            _groupByPropertyName = null;
            _groupDescription = null;
            if (Loaded) {
                SetGrouping();
            }
        }

        private void SetGrouping() {
            if (_groupByPropertyName == null || _grouped) {
                if (_groupByPropertyName == null && _grouped) {
                    _grouped = false;
                    MainList.GroupDescriptions?.Clear();
                }
                UpdateItemsMonitoring();
                return;
            }
            _grouped = true;
            MainList.GroupDescriptions?.Add(_groupDescription ?? new PropertyGroupDescription(
                    $@"Value.{_groupByPropertyName}",
                    _groupByConverter == null ? null : new ToGroupNameConverter(_groupByConverter)));
            UpdateItemsMonitoring();
        }

        private class ToGroupNameConverter : IValueConverter {
            private readonly GroupByConverter _groupByConverter;

            public ToGroupNameConverter(GroupByConverter groupByConverter) {
                _groupByConverter = groupByConverter;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value == null ? null : _groupByConverter(value.ToString());
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotImplementedException();
            }
        }

        private AcListPage _listPage;
        private bool _usePaddingForChildObjects = true;

        public bool UsePaddingForChildObjects {
            get => _usePaddingForChildObjects;
            set => Apply(value, ref _usePaddingForChildObjects, () => {
                if (_listPage != null) {
                    AcListPage.SetUsePaddingForChildObjects(_listPage, _usePaddingForChildObjects);
                }
            });
        }

        protected bool Loaded { get; private set; }

        private bool _loadedOnce;
        private bool _monitoringItems;

        private void UpdateItemsMonitoring(bool forceDisabled = false) {
            var needsMonitoring = !forceDisabled && (_listFilter != null || _grouped || _sorting != null);
            if (needsMonitoring == _monitoringItems) return;
            _monitoringItems = needsMonitoring;
            if (needsMonitoring) {
                _list.ItemPropertyChanged += OnItemPropertyChanged;
                _list.WrappedValueChanged += OnWrapperValueChanged;
            } else {
                _list.ItemPropertyChanged -= OnItemPropertyChanged;
                _list.WrappedValueChanged -= OnWrapperValueChanged;
            }
        }

        /// <summary>
        /// Don’t forget to use me!
        /// </summary>
        public virtual bool Load(AcListPage listPage) {
            _listPage = listPage;
            if (listPage != null) {
                AcListPage.SetUsePaddingForChildObjects(listPage, _usePaddingForChildObjects);
            }

            if (Loaded) return false;
            Loaded = true;
            _listPage = listPage;

            _list.CollectionChanged += OnCollectionChanged;
            _list.CollectionReady += OnCollectionReady;
            SetGrouping();

            if (!_loadedOnce) {
                _loadedOnce = true;

                using (_mainList.DeferRefresh()) {
                    if (_listFilter == null) {
                        _mainList.Filter = null;
                    } else {
                        _mainList.Filter = FilterTest;
                    }

                    _mainList.CustomSort = SortingComparer;
                }

                LoadCurrent();
                _oldNumber = _mainList.Count;
                _mainList.CurrentChanged += OnCurrentChanged;
            }
            return true;
        }

        [NotNull]
        public IComparer SortingComparer => (IComparer)_sorting ?? this;

        private AcObjectSorter<T> _sorting;

        [CanBeNull]
        public AcObjectSorter<T> Sorting {
            set {
                if (Equals(value, _sorting)) return;
                _sorting = value;
                OnPropertyChanged();

                if (Loaded) {
                    _mainList.CustomSort = SortingComparer;
                    UpdateItemsMonitoring();
                }
            }
        }

        /// <summary>
        /// Don’t forget to use me!
        /// </summary>
        public virtual bool Unload() {
            if (!Loaded) return false;
            Loaded = false;

            if (_monitoringItems) {
                _monitoringItems = false;
                _list.ItemPropertyChanged -= OnItemPropertyChanged;
                _list.WrappedValueChanged -= OnWrapperValueChanged;
            }

            _list.CollectionChanged -= OnCollectionChanged;
            _list.CollectionReady -= OnCollectionReady;
            return true;
        }

        public const string InvalidId = "";

        protected abstract string LoadCurrentId();

        protected abstract void SaveCurrentKey(string id);

        private bool _userChange = true;
        private bool _loadCurrentWaiting;

        private void LoadCurrent() {
            if (!Loaded || _mainList.IsEmpty || _loadCurrentWaiting) return;

            var selectedId = LoadCurrentId();
            if (selectedId == InvalidId) return;

            var selected = selectedId == null ? null : _manager.GetWrapperById(selectedId);
            if (selected?.IsLoaded == false) {
                _loadCurrentWaiting = true;
                selected.LoadedAsync().ContinueWith(r => ActionExtension.InvokeInMainThreadAsync(() => {
                    _mainList.MoveCurrentToOrFirst(r.Result);
                    _loadCurrentWaiting = false;
                }));
                return;
            }

            _userChange = false;
            if (_allowNonSelected) {
                _mainList.MoveCurrentToOrNull(selected?.Loaded());
            } else if (selected == null) {
                _mainList.MoveCurrentToFirst();
            } else {
                _mainList.MoveCurrentToOrFirst(selected.Loaded());
            }
            _userChange = true;
        }

        protected virtual void OnCurrentChanged(object sender, EventArgs e) {
            var obj = CurrentItem;
            if (obj == null) return;
            if (_userChange) {
                SaveCurrentKey(obj.Value.Id);
            }

            var testLaterItem = _testMeLater;
            if (testLaterItem != null) {
                _testMeLater = null;
                RefreshFilter(testLaterItem, true);
            }
        }

        protected bool FilterTest(AcPlaceholderNew o) {
            return _listFilter == null || o is T t && _listFilter.Test(t);
        }

        protected bool FilterTest(object o) {
            return _listFilter == null || o is AcItemWrapper t && t.IsLoaded && _listFilter.Test((T)t.Value);
        }

        private int _oldNumber;

        private void MainListUpdated() {
            if (!Loaded) return;

            var newNumber = _mainList.Count;
            if (_mainList.CurrentItem == null) {
                LoadCurrent();
            }

            if (newNumber == _oldNumber) return;
            FilteredNumberChanged(_oldNumber, newNumber);
            _oldNumber = newNumber;
        }

        private AcItemWrapper _testMeLater;

        private void RefreshFilter(AcPlaceholderNew obj, bool forceRefreshIfFits) {
            if (!Loaded) return;

            if (CurrentItem?.Value == obj) {
                _testMeLater = CurrentItem;
                return;
            }

            if (forceRefreshIfFits ? FilterTest(obj) : _mainList.OfType<AcItemWrapper>().Any(x => x.Value == obj) != FilterTest(obj)) {
                _list.RefreshFilter(obj);
            }
        }

        private void RefreshFilter(AcItemWrapper obj, bool forceRefreshIfFits) {
            if (!Loaded) return;

            if (CurrentItem == obj) {
                _testMeLater = CurrentItem;
                return;
            }

            if (forceRefreshIfFits ? FilterTest(obj) : _mainList.Contains(obj) != FilterTest(obj)) {
                _list.RefreshFilter(obj);
            }
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            var needsRefresh = _sorting?.IsAffectedBy(e.PropertyName) == true || _grouped && e.PropertyName == _groupByPropertyName ? 1 : 0;
            if (_listFilter != null && _listFilter.IsAffectedBy(e.PropertyName)) {
                if (needsRefresh == 0) needsRefresh = 2;
                MainListUpdated();
            }
            if (needsRefresh != 0) {
                RefreshFilter((AcPlaceholderNew)sender, needsRefresh == 1);
            }
        }

        private void OnWrapperValueChanged(object sender, WrappedValueChangedEventArgs e) {
            if (_listFilter != null) {
                RefreshFilter((AcItemWrapper)sender, true);
                MainListUpdated();
            } else if (_grouped && _collectionReady || _sorting != null) {
                RefreshFilter((AcItemWrapper)sender, true);
            }
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            MainListUpdated();
        }

        protected virtual void FilteredNumberChanged(int oldValue, int newValue) {
            if (oldValue == 0 || newValue == 0) {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        int IComparer.Compare(object x, object y) {
            return AcItemWrapper.CompareHelper(x, y);
        }

        // TODO: remove
        public bool IsEmpty => !Loaded || _mainList.IsEmpty;
    }
}