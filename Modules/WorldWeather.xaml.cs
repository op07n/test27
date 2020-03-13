using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MapDemo {
    public partial class WorldWeather : MapDemoModule, INotifyPropertyChanged {
        object selectedItem;

        public event PropertyChangedEventHandler PropertyChanged;

        public OpenWeatherMapService OpenWeatherMapService { get; set; }
        public object SelectedItem {
            get { return selectedItem; }
            set {
                if (selectedItem != value) {
                    selectedItem = value;
                    CityWeather cityWeatherInfo = selectedItem as CityWeather;
                    if (cityWeatherInfo != null && cityWeatherInfo.Forecast == null)
                        OpenWeatherMapService.GetForecastForCityAsync(cityWeatherInfo);
                    if (PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("SelectedItem"));
                }
            }
        }

        public WorldWeather() {
            InitializeComponent();
            OpenWeatherMapService = new OpenWeatherMapService();
            DataContext = this;
            OpenWeatherMapService.GetWeatherAsync();
        }

        void lbUnitType_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e) {
            OpenWeatherMapService.SetCurrentTemperatureType((TemperatureScale)e.NewValue);
        }
    }

    public class NullObjectToVisibiltyConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value == null) ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
}
