using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;

namespace gui.Services
{
    /// <summary>
    /// Simple numeric up/down control (TextBox + arrow buttons).
    /// Reliable replacement for Avalonia's NumericUpDown.
    /// </summary>
    public class SpinBox : Grid
    {
        private TextBox _text;
        private Button _up;
        private Button _down;

        private int _value = 0;
        private int _min = 0;
        private int _max = 100;

        public event EventHandler ValueChanged;

        public SpinBox()
        {
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ColumnDefinitions.Add(new ColumnDefinition(new GridLength(18)));

            _text = new TextBox
            {
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            _text.TextChanged += OnTextChanged;
            Avalonia.Controls.Grid.SetColumn(_text, 0);

            var buttons = new Grid();
            buttons.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            buttons.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            _up = new Button { Content = "▲", Padding = new Thickness(0), FontSize = 8 };
            _up.Click += (s, e) => Step(1);
            Avalonia.Controls.Grid.SetRow(_up, 0);

            _down = new Button { Content = "▼", Padding = new Thickness(0), FontSize = 8 };
            _down.Click += (s, e) => Step(-1);
            Avalonia.Controls.Grid.SetRow(_down, 1);

            buttons.Children.Add(_up);
            buttons.Children.Add(_down);
            Avalonia.Controls.Grid.SetColumn(buttons, 1);

            Children.Add(_text);
            Children.Add(buttons);

            UpdateText();
        }

        private void Step(int delta)
        {
            int v = _value + delta;
            if (v < _min) v = _min;
            if (v > _max) v = _max;
            SetValueInternal(v);
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            int v;
            if (int.TryParse(_text.Text, out v))
            {
                if (v < _min) v = _min;
                if (v > _max) v = _max;
                SetValueInternal(v, updateText: false);
            }
        }

        private void SetValueInternal(int v, bool updateText = true)
        {
            if (_value != v)
            {
                _value = v;
                if (updateText)
                    UpdateText();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateText()
        {
            if (_text != null)
                _text.Text = _value.ToString();
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = value;
                if (v < _min) v = _min;
                if (v > _max) v = _max;
                SetValueInternal(v);
            }
        }

        public int Minimum
        {
            get { return _min; }
            set { _min = value; }
        }

        public int Maximum
        {
            get { return _max; }
            set { _max = value; }
        }

        public bool IsEnabled
        {
            get { return _text.IsEnabled; }
            set
            {
                _text.IsEnabled = value;
                _up.IsEnabled = value;
                _down.IsEnabled = value;
            }
        }
    }
}
