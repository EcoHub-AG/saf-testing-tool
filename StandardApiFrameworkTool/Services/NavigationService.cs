using System;

namespace StandardApiFrameworkTool.Services
{
    public class NavigationService
    {
        private object _currentView;

        public event Action CurrentViewChanged;

        public object CurrentView
        {
            get => _currentView;
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;
                    CurrentViewChanged?.Invoke();
                }
            }
        }
    }
}
