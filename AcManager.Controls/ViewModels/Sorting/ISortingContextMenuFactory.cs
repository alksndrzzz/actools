using System;
using System.Collections.Generic;
using System.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels.Sorting {
    public interface ISortingContextMenuFactory {
        ContextMenu BuildListContextMenu([CanBeNull] Func<IEnumerable<object /* AcItemWrapper or AcObjectNew */>> itemsCallback, [CanBeNull] Action selectMultiple);
    }
}