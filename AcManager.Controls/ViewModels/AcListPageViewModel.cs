using System;
using System.Linq;
using System.Windows.Controls;
using AcManager.Controls.ViewModels.Sorting;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public abstract class AcListPageViewModel<T> : AcObjectListCollectionViewWrapper<T>, IAcListPageViewModel where T : AcObjectNew {
        private const string KeyBase = "Content";

        public IAcManagerNew Manager { get; }

        protected AcListPageViewModel([NotNull] IAcManagerNew list, IFilter<T> listFilter) : base(list, listFilter, KeyBase, false) {
            Manager = list;
            _sortingSortingMenuFactory = AcListSortingHelper.Create<T>(Key, pair => SetSortingImpl(pair.Key, pair.Value));
        }

        private AcObjectSorter<T> _curSortingImpl;
        private readonly ISortingContextMenuFactory _sortingSortingMenuFactory;

        protected void SetSortingImpl(AcObjectSorter<T> created, bool allowGrouping) {
            if (_curSortingImpl != null) {
                _curSortingImpl.OnSelected(false, false, this);
                _curSortingImpl = null;
            }
            if (created != null) {
                _curSortingImpl = created;
                _curSortingImpl.OnSelected(true, allowGrouping, this);
                UsePaddingForChildObjects = _curSortingImpl.UsePaddingForChildObjects();
                Sorting = _curSortingImpl;
            } else {
                ResetGroping();
                UsePaddingForChildObjects = true;
                Sorting = null;
            }
        }

        public ContextMenu BuildListContextMenu(Action selectMultiple) {
            return _sortingSortingMenuFactory.BuildListContextMenu(() => MainList.OfType<AcItemWrapper>(), selectMultiple);
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