using System;
using System.Collections;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.ViewModels {
    public abstract class AcObjectSorter<T> : IComparer where T : AcObjectNew {
        private bool _usePaddingForChildObjects;

        public AcObjectSorter(bool usePaddingForChildObjects = true) {
            _usePaddingForChildObjects = usePaddingForChildObjects;
        }
        
        int IComparer.Compare(object x, object y) {
            var xs = (x as AcItemWrapper)?.Value as T;
            var ys = (y as AcItemWrapper)?.Value as T;
            if (xs == null) return ys == null ? 0 : 1;
            if (ys == null) return -1;
            if (xs.Enabled != ys.Enabled) return xs.Enabled ? -1 : 1;

            var r = Compare(xs, ys);
            if (r != 0) return r;
            if (_usePaddingForChildObjects) return xs.CompareTo(ys);
            return string.Compare(xs.DisplayName, ys.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        }

        public abstract int Compare(T x, T y);

        public abstract bool IsAffectedBy(string propertyName);
        
        public bool UsePaddingForChildObjects() { return _usePaddingForChildObjects; }

        public virtual void OnSelected(bool active, bool allowGrouping, AcListPageViewModel<T> parent) { }
    }
}