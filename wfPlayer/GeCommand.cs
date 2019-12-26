using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace wfPlayer {
    public class GeCommand<T> : ICommand {
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) {
            return true;
        }
        public void Execute(object parameter) {
            _callback((T)parameter);
        }

        private bool _enabled = true;
        public bool Enabled {
            get => _enabled;
            set {
                if (_enabled != value) {
                    _enabled = true;
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private Action<T> _callback;
        public GeCommand(Action<T> callback, bool initialState = true) {
            _callback = callback;
            _enabled = true;
        }
    }

    public class GeCommand : ICommand {
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) {
            return true;
        }
        public void Execute(object parameter) {
            _callback();
        }

        private bool _enabled = true;
        public bool Enabled {
            get => _enabled;
            set {
                if (_enabled != value) {
                    _enabled = true;
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private Action _callback;
        public GeCommand(Action callback, bool initialState = true) {
            _callback = callback;
            _enabled = true;
        }
    }

}
