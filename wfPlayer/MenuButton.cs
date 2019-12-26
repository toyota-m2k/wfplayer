using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace wfPlayer {
    public class MenuButton : ToggleButton {
        public MenuButton() {
            var binding = new Binding("DropDownMenu.IsOpen") { Source = this };
            this.SetBinding(MenuButton.IsCheckedProperty, binding);
        }

        public static readonly DependencyProperty DropDownMenuProperty = DependencyProperty.Register("DropDownMenu", typeof(ContextMenu), typeof(MenuButton), new UIPropertyMetadata(null));
        public ContextMenu DropDownMenu {
            get => GetValue(DropDownMenuProperty) as ContextMenu;
            set => this.SetValue(DropDownMenuProperty, value);
        }

        protected override void OnClick() {
            if (this.DropDownMenu == null) { return; }
            this.DropDownMenu.PlacementTarget = this;
            this.DropDownMenu.Placement = PlacementMode.Bottom;
            this.DropDownMenu.IsOpen = !DropDownMenu.IsOpen;
        }
    }
}
