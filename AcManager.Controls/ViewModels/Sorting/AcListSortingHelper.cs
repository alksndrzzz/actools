using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels.Sorting {
    public static class AcListSortingHelper {
        public static ISortingContextMenuFactory Create<T>([CanBeNull] string key, Action<AcObjectSorter<T>> sortCallback) where T : AcObjectNew {
            return new SortingContextMenuFactory<T>(key, false, k => sortCallback(k.Key));
        }

        public static ISortingContextMenuFactory Create<T>([CanBeNull] string key, Action<KeyValuePair<AcObjectSorter<T>, bool>> sortCallback) where T : AcObjectNew {
            return new SortingContextMenuFactory<T>(key, true, sortCallback);
        }

        private class SortingContextMenuFactory<T> : NotifyPropertyChanged, ISortingContextMenuFactory, IValueConverter where T : AcObjectNew {
            [CanBeNull]
            private readonly string _storeKey;
            
            private readonly bool _allowGrouping;
            private readonly Action<KeyValuePair<AcObjectSorter<T>, bool>> _sortCallback;
            private ContextMenu _menu;

            public SortingContextMenuFactory([CanBeNull] string key, bool allowGrouping, Action<KeyValuePair<AcObjectSorter<T>, bool>> sortCallback) {
                _storeKey = key == null ? null : $"{key}.s";
                _allowGrouping = allowGrouping;
                _sortCallback = sortCallback;
                var sortingBy = _storeKey == null ? null : ValuesStorage.Get<string>(_storeKey);
                if (sortingBy != null) {
                    TryCreateSorting(sortingBy, false);
                }
            }

            private void TryCreateSorting(string sortingBy, bool secondary) {
                if (_sortingBy == sortingBy) return;
                var created = SortingMethodsProvider<T>.CreateByKey(sortingBy, out var withGrouping);
                if (created != null || secondary) {
                    _sortingBy = created != null ? sortingBy : string.Empty;
                    _sortingByKey = (created != null ? sortingBy.Split('/').FirstOrDefault() : null) ?? string.Empty;
                    _sortCallback(new KeyValuePair<AcObjectSorter<T>, bool>(created, withGrouping));
                    if (secondary) {
                        OnPropertyChanged(nameof(SortingBy));
                        OnPropertyChanged(nameof(SortingByKey));
                    }
                }
            }

            private string _sortingBy = string.Empty;

            public string SortingBy {
                get => _sortingBy;
                set => Apply(value, ref _sortingBy);
            }

            private string _sortingByKey = string.Empty;

            public string SortingByKey {
                get => _sortingByKey;
                set => Apply(value, ref _sortingByKey);
            }

            public ContextMenu BuildListContextMenu(Func<IEnumerable<object>> itemsCallback, Action selectMultiple) {
                if (_menu != null) {
                    return _menu;
                }
                
                var setSorting = new DelegateCommand<string>(key => {
                    TryCreateSorting(key, true);
                    if (_storeKey != null) {
                        ValuesStorage.Set(_storeKey, key);
                    }
                });
                var sortMenu = new MenuItem { Header = "Sort by…" };
                foreach (var kv in SortingMethodsProvider<T>.GetSortingTypes()) {
                    if (kv == null) {
                        sortMenu.Items.Add(new Separator());
                    } else {
                        if (kv.WithGrouping && _allowGrouping) {
                            var variants = new MenuItem {
                                Header = kv.DisplayName,
                                Items = {
                                    BuildSortingItem("Without grouping", kv.Id),
                                    BuildSortingItem("With grouping", $@"{kv.Id}/g"),
                                }
                            };
                            sortMenu.Items.Add(variants);
                            variants.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(SortingByKey)) {
                                Source = this,
                                Converter = this,
                                ConverterParameter = kv.Id
                            });
                        } else {
                            sortMenu.Items.Add(BuildSortingItem(kv.DisplayName, kv.Id));
                        }
                    }
                }

                var menu = new ContextMenu();
                _menu = menu;
                menu.Items.Add(sortMenu);
                if (itemsCallback != null) {
                    var copyIDs = new DelegateCommand(() => ClipboardHelper.SetText(itemsCallback().Select(x => {
                        if (x is AcItemWrapper w) return w.Id;
                        if (x is AcObjectNew o) return o.Id;
                        return null;
                    }).NonNull().JoinToString('\n')));
                    var copyTags = new DelegateCommand(() => ClipboardHelper.SetText(itemsCallback().Select(x => {
                        if (x is AcItemWrapper w) return w.Value as AcJsonObjectNew;
                        if (x is AcObjectNew o) return o as AcJsonObjectNew;
                        return null;
                    }).NonNull().SelectMany(x => x.Tags).OrderBy(x => x).Distinct().JoinToString('\n')));
                    menu.Items.Add(new Separator());
                    menu.Items.Add(new MenuItem { Header = "Copy IDs", Command = copyIDs });
                    menu.Items.Add(new MenuItem { Header = "Copy tags", Command = copyTags });
                }
                if (selectMultiple != null) {
                    menu.Items.Add(new Separator());
                    menu.Items.Add(new MenuItem {
                        Header = "Select multiple…",
                        Command = new DelegateCommand(selectMultiple),
                        ToolTip = "Alternatively, you can hold Ctrl or Shift and click a second item in the list"
                    });
                }
                return menu;

                MenuItem BuildSortingItem(string displaySort, string sortArg) {
                    var ret = new MenuItem { Header = displaySort, Command = setSorting, CommandParameter = sortArg, };
                    ret.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(SortingBy)) {
                        Source = this,
                        Converter = this,
                        ConverterParameter = sortArg
                    });
                    return ret;
                }
            }

            object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value as string == parameter as string;
            }

            object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                return null;
            }
        }
    }
}