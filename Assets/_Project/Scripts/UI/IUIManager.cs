namespace SmashFest.UI
{
    public interface IUIManager
    {
        T ShowPanel<T>() where T : UIPanelBase;

        T ShowPopup<T>() where T : UIPanelBase;

        void HidePopup<T>() where T : UIPanelBase;

        void HidePopups();
    }
}
