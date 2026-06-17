using System;
using System.Windows.Controls;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;

namespace AcManager.Controls.ViewModels {
    public interface IAcListPageViewModel {
        string GetNumberString(int count);
        string Status { get; }
        void SetCurrentItem(string id);
        AcWrapperCollectionView GetAcWrapperCollectionView();
        IAcManagerNew Manager { get; }
        ContextMenu BuildListContextMenu(Action selectMultiple);
    }
}