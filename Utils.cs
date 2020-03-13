using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using DevExpress.Mvvm;
using DevExpress.Xpf.Map;

namespace MapDemo {
    public enum TemperatureScale { Fahrenheit, Celsius };

    public class DemoValuesProvider {
        const string key = DevExpress.Map.Native.DXBingKeyVerifier.BingKeyWpfMapDemo;

        public string DevexpressBingKey { get { return key; } }
        public IEnumerable<BingMapKind> BingMapKinds { get { return new BingMapKind[] { BingMapKind.Area, BingMapKind.Road, BingMapKind.Hybrid }; } }
        public IEnumerable<OpenStreetMapKind> OSMBaseLayers { get { return new OpenStreetMapKind[] { OpenStreetMapKind.Basic, OpenStreetMapKind.CycleMap, OpenStreetMapKind.Hot, OpenStreetMapKind.GrayScale, OpenStreetMapKind.Transport }; } }
        public IEnumerable<object> OSMOverlayLayers { get { return new object[] { "None", OpenStreetMapKind.SeaMarks, OpenStreetMapKind.HikingRoutes, OpenStreetMapKind.CyclingRoutes, OpenStreetMapKind.PublicTransport }; } }
        public IEnumerable<string> ShapeMapTypes { get { return new string[] { "GDP", "Population", "Political" }; } }
        public IEnumerable<string> ShapefileMapTypes { get { return new string[] { "World", "Africa", "South America", "North America", "Australia", "Eurasia" }; } }

        public IEnumerable<TemperatureScale> TemperatureUnit { get { return new TemperatureScale[] { TemperatureScale.Celsius, TemperatureScale.Fahrenheit }; } }
        public IEnumerable<MarkerType> BubbleMarkerTypes { get { return new MarkerType[] { MarkerType.Circle, MarkerType.Cross, MarkerType.Diamond, MarkerType.Hexagon,
                                                                MarkerType.InvertedTriangle, MarkerType.Triangle, MarkerType.Pentagon, MarkerType.Plus,
                                                                MarkerType.Square, MarkerType.Star5, MarkerType.Star6, MarkerType.Star8 }; } }
        public IEnumerable<ProjectionBase> ProjectionTypes { get {
                return new ProjectionBase[] { new SphericalMercatorProjection(), new EqualAreaProjection(), new EquirectangularProjection(),
                    new EllipticalMercatorProjection(), new MillerProjection(), new EquidistantProjection(), new LambertCylindricalEqualAreaProjection(),
                    new BraunStereographicProjection(), new KavrayskiyProjection(), new SinusoidalProjection(), new EPSG4326Projection() };
            }
        }
    }

    public static class DemoUtils {
        public static IEnumerable<MapControl> FindMap(DependencyObject obj) {
            if(obj != null) {
                for(int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) {
                    DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                    if(child != null && child is MapControl) {
                        yield return (MapControl)child;
                    }
                    foreach(MapControl childOfChild in FindMap(child)) {
                        yield return childOfChild;
                    }
                }
            }
        }
    }

    public static class DataLoader {
        static Stream GetStream(string fileName) {
            fileName = "/MapDemo;component" + fileName;
            Uri uri = new Uri(fileName, UriKind.RelativeOrAbsolute);
            return Application.GetResourceStream(uri).Stream;
        }

        public static XDocument LoadXmlFromResources(string fileName) {
            try {
                return XDocument.Load(GetStream(fileName));
            } catch {
                return null;
            }
        }
        public static Stream LoadStreamFromResources(string fileName) {
            try {
                return GetStream(fileName);
            } catch {
                return null;
            }
        }
    }

    public class DoubleToTimeSpanConvert : IValueConverter {
        #region IValueConvector implementation
        object IValueConverter.Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            double doubleValue = 3600 * (double)value;
            return new TimeSpan(0, 0, (int)Math.Ceiling(doubleValue));
        }
        object IValueConverter.ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return null;
        }
        #endregion
    }
    public class SelectedLayerToVisibilityConverter : IValueConverter {
        #region IValueConverter implementation
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return value is string ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return null;
        }
        #endregion
    }
    public class SelectedLayerToKindConverter : IValueConverter {
        #region IValueConverter implementation
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return value is string ? OpenStreetMapKind.SeaMarks : value;
        }
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return null;
        }
        #endregion
    }
    public class PlaneInfoToPathVisibilityConverter : IValueConverter {
        #region IValueConverter implementation
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return value == parameter;
        }
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return null;
        }
        #endregion
    }

    public class RangeColor {
        readonly int rangeMin;
        readonly int rangeMax;
        readonly Color fill;

        public int RangeMin {
            get { return rangeMin; }
        }
        public int RangeMax {
            get { return rangeMax; }
        }
        public Color Fill {
            get { return fill; }
        }

        public RangeColor(int rangeMin, int rangeMax, Color fill) {
            this.rangeMin = rangeMin;
            this.rangeMax = rangeMax;
            this.fill = fill;
        }
    }

    public class ViewTypeToBoolConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(targetType == typeof(bool) && value is ViewType && parameter is ViewType)
                return (ViewType)value == (ViewType)parameter;
            return false;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is bool)
                if(targetType == typeof(ViewType)) {
                    return (bool)value ? ViewType.Gallery : ViewType.Map;
                }
            return null;
        }
    }

    public class ViewTypeToVisibilityConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (targetType == typeof(Visibility) && value is ViewType && parameter is ViewType)
                return (ViewType)value == (ViewType)parameter ? Visibility.Visible : Visibility.Hidden;
            return Visibility.Hidden;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is Visibility)
                if (targetType == typeof(ViewType)) {
                    return (Visibility)value == Visibility.Visible ? ViewType.Gallery : ViewType.Map;
                }
            return null;
        }
    }
    public class CoordinateSystemTypeToVisibilityConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(targetType == typeof(Visibility) && value is CoordinateSystemType && parameter is CoordinateSystemType)
                return (CoordinateSystemType)value == (CoordinateSystemType)parameter ? Visibility.Visible : Visibility.Hidden;
            return Visibility.Hidden;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is Visibility)
                if(targetType == typeof(CoordinateSystemType))
                    return (Visibility)value == Visibility.Visible ? CoordinateSystemType.Geo : CoordinateSystemType.Cartesian;
            return null;
        }
    }
    public class DoubleToRenderTransforOffsetConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (targetType == typeof(double) && value is double) {
                double doubleValue = (double)value;
                if (parameter is double)
                    return doubleValue / (double)parameter;
                return 0;
            }
            return null;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class CoordinateSystemTypeToCoordinateSystemConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (targetType == typeof(MapCoordinateSystem) && value is CoordinateSystemType && (CoordinateSystemType)value == CoordinateSystemType.Cartesian)
                return new CartesianMapCoordinateSystem();
            return new GeoMapCoordinateSystem();
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is MapCoordinateSystem && targetType == typeof(CoordinateSystemType))
                return value is GeoMapCoordinateSystem ? CoordinateSystemType.Geo : CoordinateSystemType.Cartesian;
            return null;
        }
    }
    public class ItemToTextConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is MapItem) {
                MapPath path = value as MapPath;
                if(path != null)
                    return HotelRoomTooltipHelper.CalculateTitle(path);
                ShapeTitle title = value as ShapeTitle;
                if(title != null)
                    return HotelRoomTooltipHelper.CalculateTitle(title.MapShape);
            }
            return null;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class ItemToImageSourceConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is MapItem) {
                ShapeTitle title = value as ShapeTitle;
                return HotelRoomTooltipHelper.GetItemImageSource(title != null ? title.MapShape : (MapItem)value);
            }
            return null;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class ItemToImageVisibilityConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is MapItem && targetType == typeof(Visibility)) {
                ShapeTitle title = value as ShapeTitle;
                string imageSource = HotelRoomTooltipHelper.GetItemImageSource(title != null ? title.MapShape : (MapItem)value);
                return string.IsNullOrWhiteSpace(imageSource) ? Visibility.Collapsed : Visibility.Visible;
            }
            return null;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class CountToMatrixTransformConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            List<MapItem> itemsList = value as List<MapItem>;
            double count = itemsList != null ? itemsList.Count : 1.0;
            double scaleFactor = Math.Log10(count / 5.0) * 0.02 + 0.05;
            double offsetKoefX = 318;
            double offsetKoefY = -455;
            return new MatrixTransform(scaleFactor, 0, 0, scaleFactor, scaleFactor * offsetKoefX, scaleFactor * offsetKoefY).Matrix;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class CountToTextConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            List<MapItem> itemsList = value as List<MapItem>;
            int count = itemsList != null ? itemsList.Count : 1;
            return string.Format("Cluster contains {0} item(s)", count);
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class MapTypeToVisibilityConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is int && targetType == typeof(Visibility) && parameter is string) {
                int index = (int)value;
                string mapType = (string)parameter;
                if(index == 1 && mapType == "population")
                    return Visibility.Visible;
                if(index == 0 && mapType == "gdp")
                    return Visibility.Visible;
                return Visibility.Collapsed;
            }
            return null;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class ProviderNameToImageVisibilityConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is ProviderName && targetType == typeof(Visibility)) {
                return (ProviderName)value == ProviderName.Bing ? Visibility.Visible : Visibility.Collapsed;
            }
            return null;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class ProviderNameToCopyrightTextConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is ProviderName) {
                ProviderName providerName = (ProviderName)value;
                if(providerName == ProviderName.Bing)
                    return "Copyright © 2018 Microsoft and its suppliers. All rights reserved.";
                if(providerName == ProviderName.Osm)
                    return "© OpenStreetMap contributors";
                return null;
            }
            return null;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
    public class BoolToCircularScrollingConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is bool && targetType == typeof(CircularScrollingMode)) {
                return (bool)value ? CircularScrollingMode.TilesAndVectorItems : CircularScrollingMode.None;
            }
            return null;
        }
        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    public class ShapefileWorldResources {
        public Uri CountriesFileUri { get { return new Uri("/MapDemo;component/Data/Shapefiles/Maps/Countries.shp", UriKind.RelativeOrAbsolute); } }
        public Uri AfricaFileUri { get { return new Uri("/MapDemo;component/Data/Shapefiles/Maps/Africa.shp", UriKind.RelativeOrAbsolute); } }
        public Uri SouthAmericaFileUri { get { return new Uri("/MapDemo;component/Data/Shapefiles/Maps/SouthAmerica.shp", UriKind.RelativeOrAbsolute); } }
        public Uri NorthAmericaFileUri { get { return new Uri("/MapDemo;component/Data/Shapefiles/Maps/NorthAmerica.shp", UriKind.RelativeOrAbsolute); } }
        public Uri AustraliaFileUri { get { return new Uri("/MapDemo;component/Data/Shapefiles/Maps/Australia.shp", UriKind.RelativeOrAbsolute); } }
        public Uri EurasiaFileUri { get { return new Uri("/MapDemo;component/Data/Shapefiles/Maps/Eurasia.shp", UriKind.RelativeOrAbsolute); } }

        public ShapefileWorldResources() {
        }
    }
    
    public class PhotoGalleryResources {
        public BitmapImage CityInformationControlSource { get { return new BitmapImage(new Uri("/MapDemo;component/Images/PhotoGallery/CityInformationControl.png", UriKind.RelativeOrAbsolute)); } }
        public BitmapImage LabelControlImageSource { get { return new BitmapImage(new Uri("/MapDemo;component/Images/PhotoGallery/Label.png", UriKind.RelativeOrAbsolute)); } }
        public BitmapImage PlaceInfoControlPrevImageSource { get { return new BitmapImage(new Uri("/MapDemo;component/Images/PhotoGallery/PrevPlace.png", UriKind.RelativeOrAbsolute)); } }
        public BitmapImage PlaceInfoControlNextImageSource { get { return new BitmapImage(new Uri("/MapDemo;component/Images/PhotoGallery/NextPlace.png", UriKind.RelativeOrAbsolute)); } }

        public PhotoGalleryResources() {
        }
    }

    public class CityWeather : BindableBase {
        OpenWeatherMapService.CityWeatherInfo cityWeatherInfo;
        Weather weatherCore;
        List<WeatherDescription> weatherDescriptionsCore;
        string weatherIconPath = string.Empty;
        ObservableCollection<CityWeather> forecast = new ObservableCollection<CityWeather>(); 
        string temperatureValueDataMember = string.Empty;
        string temperatureString = string.Empty;
        string crosshairLabelPattern = string.Empty;

        public DateTime Day { get { return GetTime(cityWeatherInfo.Day); } }
        public int CityID { get { return cityWeatherInfo.Id; } }
        public string City { get { return cityWeatherInfo.Name; } }
        public double Longitude { get { return cityWeatherInfo.Coord.Longitude; } }
        public double Latitude { get { return cityWeatherInfo.Coord.Latitude; } }
        public Weather Weather { get { return weatherCore; } }
        public List<WeatherDescription> WeatherDescriptions { get { return weatherDescriptionsCore; } }
        public DateTime ForecastTime { get; set; }

        public string WeatherIconPath {
            get { return GetProperty(() => weatherIconPath); }
            set { SetProperty(() => weatherIconPath, value); }
        }
        public ObservableCollection<CityWeather> Forecast {
            get { return GetProperty(() => forecast); }
            set { SetProperty(() => forecast, value); }
        }
        public string TemperatureValueDataMember {
            get { return GetProperty(() => temperatureValueDataMember); }
            set { SetProperty(() => temperatureValueDataMember, value); }
        }
        public string TemperatureString {
            get { return GetProperty(() => temperatureString); }
            set { SetProperty(() => temperatureString, value); }
        }
        public string CrosshairLabelPattern {
            get { return GetProperty(() => crosshairLabelPattern); }
            set { SetProperty(() => crosshairLabelPattern, value); }
        }

        public CityWeather(OpenWeatherMapService.CityWeatherInfo cityWeatherInfo) {
            this.cityWeatherInfo = cityWeatherInfo;
            this.weatherCore = new Weather(cityWeatherInfo.Main);
            this.weatherDescriptionsCore = new List<WeatherDescription>();
            foreach(OpenWeatherMapService.WeatherDescriptionInfo weatherDescription in cityWeatherInfo.Weather)
                weatherDescriptionsCore.Add(new WeatherDescription(weatherDescription));
        }

        DateTime GetTime(long seconds) {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return dtDateTime.AddSeconds(Convert.ToDouble(seconds)).ToLocalTime();
        }
        public void SetForecast(ObservableCollection<OpenWeatherMapService.CityWeatherInfo> forecast) {
            ObservableCollection<CityWeather> cityWeatherList = new ObservableCollection<CityWeather>();
            foreach(OpenWeatherMapService.CityWeatherInfo cityWeatherInfo in forecast)
                cityWeatherList.Add(new CityWeather(cityWeatherInfo));
            Forecast = cityWeatherList;
        }
        public void SetCurrentTemperatureType(TemperatureScale temperatureScale) {
            switch(temperatureScale) {
                case TemperatureScale.Fahrenheit:
                    TemperatureValueDataMember = "Weather.FahrenheitTemperature";
                    TemperatureString = Weather.FahrenheitTemperatureString;
                    CrosshairLabelPattern = "{A:g} : {V} °F";
                    break;
                case TemperatureScale.Celsius:
                    TemperatureValueDataMember = "Weather.CelsiusTemperature";
                    TemperatureString = Weather.CelsiusTemperatureString;
                    CrosshairLabelPattern = "{A:g} : {V} °C";
                    break;
            }
        }
    }
    public class Weather {
        OpenWeatherMapService.WeatherInfo weatherInfo;

        public int CelsiusTemperature { get { return (int)weatherInfo.Temp; } }
        public int FahrenheitTemperature { get { return CelsiusTemperature * 9 / 5 + 32; } }
        public int KelvinTemperature { get { return (int)weatherInfo.Temp; } }
        public string CelsiusTemperatureString { get { return CelsiusTemperature.ToString("+#;-#;0") + " °C"; } }
        public string FahrenheitTemperatureString { get { return FahrenheitTemperature.ToString("+#;-#;0") + " °F"; } }
        public string KelvinTemperatureString { get { return weatherInfo.Temp.ToString("+#;-#;0") + " °K"; } }

        public Weather(OpenWeatherMapService.WeatherInfo weatherInfo) {
            this.weatherInfo = weatherInfo;
        }
    }
    public class WeatherDescription {
        OpenWeatherMapService.WeatherDescriptionInfo weatherDescriptionInfo;

        public string IconName { get { return weatherDescriptionInfo.Icon; } }

        public WeatherDescription(OpenWeatherMapService.WeatherDescriptionInfo weatherDescriptionInfo) {
            this.weatherDescriptionInfo = weatherDescriptionInfo;
        }
    }
    public class OpenWeatherMapService : BindableBase {
        #region classes for JSON parsing

        [DataContract]
        public class ForecastInfo {
            [DataMember]
            public ObservableCollection<CityWeatherInfo> list;
        }
        [DataContract]
        public class WorldWeatherInfo {
            [DataMember]
            public ObservableCollection<CityWeatherInfo> list;
        }
        [DataContract]
        public class CityWeatherInfo {
            [DataMember(Name = "id")]
            public int Id { get; set; }
            [DataMember(Name = "name")]
            public string Name { get; set; }
            [DataMember(Name = "coord")]
            public Coordinates Coord { get; set; }
            [DataMember(Name = "main")]
            public WeatherInfo Main { get; set; }
            [DataMember(Name = "weather")]
            public List<WeatherDescriptionInfo> Weather { get; set; }
            [DataMember(Name = "wind")]
            public WindInfo Wind { get; set; }
            [DataMember(Name = "dt")]
            public long Day { get; set; }
        }
        [DataContract]
        public class WeatherDescriptionInfo {
            [DataMember(Name = "main")]
            public string Main { get; set; }
            [DataMember(Name = "description")]
            public string Description { get; set; }
            [DataMember(Name = "icon")]
            public string Icon { get; set; }
        }
        [DataContract]
        public class WindInfo {
            [DataMember(Name = "speed")]
            public double Speed { get; set; }
            [DataMember(Name = "deg")]
            public double Deg { get; set; }
        }
        [DataContract]
        public class WeatherInfo {
            [DataMember(Name = "temp")]
            public double Temp { get; set; }
            [DataMember(Name = "pressure")]
            public double Pressure { get; set; }
            [DataMember(Name = "humidity")]
            public double Humidity { get; set; }
        }
        [DataContract]
        public class Coordinates {
            [DataMember(Name = "lon")]
            double lon1;
            [DataMember(Name = "Lon")]
            double lon2;
            [DataMember(Name = "lat")]
            double lat1;
            [DataMember(Name = "Lat")]
            double lat2;

            #region warnings workarond
            protected double Lat1 { set { this.lat1 = value; } }
            protected double Lat2 { set { this.lat2 = value; } }
            protected double Lon1 { set { this.lon1 = value; } }
            protected double Lon2 { set { this.lon2 = value; } }
            #endregion
            public double Longitude { get { return lon1 != 0 ? lon1 : lon2; } }
            public double Latitude { get { return lat1 != 0 ? lat1 : lat2; } }
        }

        #endregion

        const string OpenWeatherKey = "fcbff6dbed7bd7f295489daf4ffef3f1";

        TemperatureScale temperatureScale = TemperatureScale.Celsius;
        object weatherLocker = new object();
        List<string> capitals = new List<string>();
        ObservableCollection<CityWeather> weatherInCities = new ObservableCollection<CityWeather>();

        public ObservableCollection<CityWeather> WeatherInCities {
            get { return GetProperty(() => weatherInCities); }
            set { SetProperty(() => weatherInCities, value); }
        }
        public ObservableCollection<CityWeather> Forecast { get; set; }

        readonly Dispatcher uiDispatcher;

        public OpenWeatherMapService() {
            LoadCapitalsFromXML();
            uiDispatcher = Dispatcher.CurrentDispatcher;
        }

        void weatherClient_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {
            if(e.Error == null) {
                Task.Factory.StartNew(() => {
                    DataContractJsonSerializer dc = new DataContractJsonSerializer(typeof(WorldWeatherInfo));
                    WorldWeatherInfo worldWeatherInfo = (WorldWeatherInfo)dc.ReadObject(e.Result);
                    ObservableCollection<CityWeather> tempWeatherInCities = new ObservableCollection<CityWeather>();
                    foreach(CityWeatherInfo weatherInfo in worldWeatherInfo.list) {
                        CityWeather cityWeather = new CityWeather(weatherInfo);
                        string cityWithId = string.Format("{0};{1}", cityWeather.City, cityWeather.CityID);
                        if(capitals.Contains(cityWeather.City) || capitals.Contains(cityWithId)) {
                            if(cityWeather.WeatherDescriptions != null && cityWeather.WeatherDescriptions.Count > 0)
                                cityWeather.WeatherIconPath = "http://openweathermap.org/img/w/" + cityWeather.WeatherDescriptions[0].IconName + ".png";
                            tempWeatherInCities.Add(cityWeather);
                        }
                    }
                    lock(weatherLocker) {
                        WeatherInCities = tempWeatherInCities;
                    }
                    this.uiDispatcher.Invoke(new Action(() => UpdateCurrentTemperatureType()));
                });
            }
        }
        void forecastClient_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {
            if(e.Error == null) {
                ((WebClient)sender).OpenReadCompleted -= forecastClient_OpenReadCompleted;
                Stream stream = e.Result;
                CityWeather cityWeatherInfo = (CityWeather)e.UserState;
                Task.Factory.StartNew(() => {
                    DataContractJsonSerializer dc = new DataContractJsonSerializer(typeof(ForecastInfo));
                    ForecastInfo forecast = (ForecastInfo)dc.ReadObject(stream);
                    this.uiDispatcher.Invoke(new Action(() => { cityWeatherInfo.SetForecast(forecast.list); })); 
                });
            }
        }
        void LoadCapitalsFromXML() {
            XDocument document = DataLoader.LoadXmlFromResources("/Data/Capitals.xml");
            if(document != null) {
                foreach(XElement element in document.Element("Capitals").Elements())
                    capitals.Add(element.Value);
            }
        }
        void UpdateCurrentTemperatureType() {
            lock(weatherLocker) {
                if(WeatherInCities != null) {
                    foreach(CityWeather weather in WeatherInCities)
                        weather.SetCurrentTemperatureType(temperatureScale);
                }
            }
        }
        public void GetWeatherAsync() {
            string link = "http://api.openweathermap.org/data/2.5/box/city?bbox=-180,-90,180,90&cluster=yes&APPID=" + OpenWeatherKey;
            WebClient weatherClient = new WebClient();
            weatherClient.OpenReadCompleted += weatherClient_OpenReadCompleted;
            weatherClient.OpenReadAsync(new Uri(link));
        }
        public void GetForecastForCityAsync(CityWeather cityWeather) {
            string link = string.Format("http://api.openweathermap.org/data/2.5/forecast?units=metric&id={0}&APPID={1}", cityWeather.CityID.ToString(), OpenWeatherKey);
            WebClient forecastClient = new WebClient();
            forecastClient.OpenReadCompleted += forecastClient_OpenReadCompleted;
            forecastClient.OpenReadAsync(new Uri(link), cityWeather);
        }
        public void SetCurrentTemperatureType(TemperatureScale temperatureScale) {
            this.temperatureScale = temperatureScale;
            UpdateCurrentTemperatureType();
        }
    }
    public static class HotelRoomTooltipHelper {
        #region inner class
        class HotelImagesGenerator {
            class PathsIndexPair {
                public string[] Paths { get; set; }
                public int Index { get; set; }
            }

            static readonly string[] Categories = new string[] { "Restaurant", "MeetingRoom", "Bathroom", "Bedroom", "Outofdoors", "ServiceRoom", "Pool", "Lobby" };
            const string basePath = "/MapDemo;component/";

            int hotelIndex = 0;
            List<PathsIndexPair> filesWithIndices = new List<PathsIndexPair>();

            public int HotelIndex {
                get { return hotelIndex; }
                set {
                    hotelIndex = value;
                    UpdateIndices();
                }
            }

            public HotelImagesGenerator() {
                foreach(string category in Categories)
                    filesWithIndices.Add(new PathsIndexPair() { Index = 0, Paths = GetAvailableFiles(category) });
            }
            void UpdateIndices() {
                filesWithIndices[0].Index = hotelIndex * 2;
                filesWithIndices[1].Index = 0;
                filesWithIndices[2].Index = hotelIndex * 4;
                filesWithIndices[6].Index = hotelIndex;
            }
            string[] GetAvailableFiles(string category) {
                var asm = Assembly.GetExecutingAssembly();
                string resName = asm.GetName().Name + ".g.resources";
                using(var stream = asm.GetManifestResourceStream(resName))
                using(var reader = new System.Resources.ResourceReader(stream)) {
                    return reader.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Where(entry => entry.StartsWith("images/hotels/" + category.ToLowerInvariant())).ToArray();
                }
            }
            string GetImagePath(int category, string name, int roomCat) {
                if(category == 4)
                    filesWithIndices[3].Index = roomCat;
                return GetCategoryImagePath(filesWithIndices[category - 1]);
            }
            string GetCategoryImagePath(PathsIndexPair pathsWithIndex) {
                if(pathsWithIndex.Paths.Length == 0)
                    return null;
                int index = pathsWithIndex.Index % pathsWithIndex.Paths.Length;
                pathsWithIndex.Index++;
                return pathsWithIndex.Paths[index];
            }
            public string GetItemImagePath(MapItem item) {
                string imagePath = GetImagePath((int)item.Attributes["CATEGORY"].Value, item.Attributes["NAME"].Value.ToString(), (int)item.Attributes["ROOMCAT"].Value);
                if(imagePath == null)
                    return null;
                string totalPath = basePath + imagePath;
                item.Attributes.Add(new MapItemAttribute() { Name = "IMAGESOURCE", Value = totalPath });
                return totalPath;
            }
        }
        #endregion

        static HotelImagesGenerator imagesGenerator = new HotelImagesGenerator();

        public static string CalculateTitle(MapItem item) {
            int category = (int)item.Attributes["CATEGORY"].Value;
            string text = (string)item.Attributes["NAME"].Value;
            return category == 4 ? string.Format("Room: {0}", text) : text;
        }
        public static string GetItemImageSource(MapItem item) {
            if(item == null)
                return null;
            MapItemAttribute attr = item.Attributes["IMAGESOURCE"];
            return attr != null ? (string)attr.Value : imagesGenerator.GetItemImagePath(item);
        }
        public static void UpdateHotelIndex(int index) {
            imagesGenerator.HotelIndex = index;
        }
    }
}
