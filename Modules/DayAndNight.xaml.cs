using DevExpress.Demos.DayAndNightLineCalculator;
using DevExpress.Mvvm;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Map;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.POCO;

namespace MapDemo {
    public partial class DayAndNight : MapDemoModule {
        DayAndNightViewModel viewModel;

        DayAndNightViewModel ViewModel { get { return viewModel; } }

        public DayAndNight() {
            InitializeComponent();
            this.viewModel = ViewModelSource.Create(() => new DayAndNightViewModel(Map));
            this.DataContext = viewModel;
        }
        void Button_Click(object sender, RoutedEventArgs e) {
            ViewModel.SetCurrentDateTime();
        }
        void ButtonBackwardClick(object sender, RoutedEventArgs e) {
            ViewModel.SetPreviousDateTime();
        }
        void ButtonForwardClick(object sender, RoutedEventArgs e) {
            ViewModel.SetNextDateTime();
        }
        void lbProjection_SelectedIndexChanged(object sender, RoutedEventArgs e) {
            ViewModel.ZoomToFit();
        }
    }
    [POCOViewModel]
    public class DayAndNightViewModel : BindableBase {
        const double DiscreteHoursStep = 0.5;
        const double SteadilyHoursStep = 24.5;
        readonly MapControl map;
        DispatcherTimer timer;

        protected MapControl Map { get { return map; } }
        public virtual GeoPoint SunPosition { get; set; }
        public virtual GeoPoint MoonPosition { get; set; }
        public virtual CoordPointCollection DayAndNightLineVertices { get; set; }
        public virtual bool IsSteady { get; set; }
        public virtual object DataContext { get; set; }

        protected void OnIsSteadyChanged() {
            this.timer.IsEnabled = IsSteady;
        }
        public virtual DateTime ActualDateTime { get; set; }
        protected void OnActualDateTimeChanged() {
            UpdateDayAndNightLine();
        }

        public DayAndNightViewModel(MapControl map) {
            this.map = map;
            InitializeTimer();
            Map.Layers[0].Loaded += DayAndNightViewModel_Loaded;
            Map.Layers[0].Unloaded += DayAndNightViewModel_Unloaded;
            IsSteady = true;
            SunPosition = new GeoPoint();
            MoonPosition = new GeoPoint();
            DayAndNightLineVertices = new CoordPointCollection();
            ActualDateTime = DateTime.UtcNow;
        }
        void InitializeTimer() {
            this.timer = new DispatcherTimer();
            this.timer.Interval = TimeSpan.FromMilliseconds(100);
            this.timer.IsEnabled = true;
            this.timer.Tick += timer_Tick;
        }
        void UpdateDayAndNightLine() {
            double[] sun3DPosition = DayAndNightLineCalculator.CalculateSunPosition(ActualDateTime);
            GeoPoint sunPosition = new GeoPoint(sun3DPosition[1], sun3DPosition[0]);
            GeoPoint moonPosition = GetOppositePoint(sunPosition);
            CoordPointCollection dayAndNightLineVertices = GetdayAndNightLineVertices(sunPosition, 0.1);
            bool isNorthNight = DayAndNightLineCalculator.CalculateIsNorthNight(sun3DPosition);
            if (isNorthNight)
                AddNorthContour(dayAndNightLineVertices);
            else
                AddSouthContour(dayAndNightLineVertices);
            SunPosition = sunPosition;
            MoonPosition = moonPosition;
            DayAndNightLineVertices = dayAndNightLineVertices;
        }
        GeoPoint GetOppositePoint(GeoPoint sunLocation) {
            double lat = -sunLocation.Latitude;
            double lon = sunLocation.Longitude + 180;
            if (lon > 180)
                lon -= 360;
            return new GeoPoint(lat, lon);
        }
        CoordPointCollection GetdayAndNightLineVertices(GeoPoint sunLocation, double step) {
            CoordPointCollection result = new CoordPointCollection();
            IList<double> latitudes = DayAndNightLineCalculator.GetDayAndNightLineLatitudes(sunLocation.Latitude, sunLocation.Longitude, step);
            double lon = -180;
            foreach (double lat in latitudes) {
                result.Add(new GeoPoint(lat, lon));
                lon += step;
            }
            return result;
        }
        void AddNorthContour(CoordPointCollection dayAndNightLineVertices) {
            double initLat = Math.Ceiling(((GeoPoint)dayAndNightLineVertices[dayAndNightLineVertices.Count - 1]).Latitude);
            for (double latForward = initLat; latForward <= 90.0; latForward++)
                dayAndNightLineVertices.Add(new GeoPoint(latForward, 180));
            for (double lon = 180; lon >= -180; lon--)
                dayAndNightLineVertices.Add(new GeoPoint(90, lon));
            initLat = Math.Ceiling(((GeoPoint)dayAndNightLineVertices[0]).Latitude);
            for (double latBackward = 90; latBackward >= initLat; latBackward--)
                dayAndNightLineVertices.Add(new GeoPoint(latBackward, -180));
        }
        void AddSouthContour(CoordPointCollection dayAndNightLineVertices) {
            double initLat = Math.Ceiling(((GeoPoint)dayAndNightLineVertices[dayAndNightLineVertices.Count - 1]).Latitude);
            for (double lat = initLat; lat >= -90.0; lat--)
                dayAndNightLineVertices.Add(new GeoPoint(lat, 180));
            for (double lon = 180; lon >= -180; lon--)
                dayAndNightLineVertices.Add(new GeoPoint(-90, lon));
            initLat = Math.Ceiling(((GeoPoint)dayAndNightLineVertices[0]).Latitude);
            for (double lat = -90; lat <= initLat; lat++)
                dayAndNightLineVertices.Add(new GeoPoint(lat, -180));
        }
        DateTime GetNextDateTime(DateTime dt) {
            return dt.AddHours(IsSteady ? SteadilyHoursStep : DiscreteHoursStep);
        }
        DateTime GetPreviousDateTime(DateTime dt) {
            return dt.AddHours(-DiscreteHoursStep);
        }
        public void SetCurrentDateTime() {
            IsSteady = false;
            ActualDateTime = DateTime.UtcNow;
        }
        public void SetPreviousDateTime() {
            IsSteady = false;
            ActualDateTime = GetPreviousDateTime(ActualDateTime);
        }
        public void SetNextDateTime() {
            IsSteady = false;
            ActualDateTime = GetNextDateTime(ActualDateTime);
        }
        public void ZoomToFit() {
            Map.EnableZooming = true;
            Map.ZoomToFitLayerItems(0.3);
            Map.EnableZooming = false;
        }
        void timer_Tick(object sender, EventArgs e) {
            ActualDateTime = GetNextDateTime(ActualDateTime);
        }
        void DayAndNightViewModel_Loaded(object sender, RoutedEventArgs e) {
            ZoomToFit();
        }
        void DayAndNightViewModel_Unloaded(object sender, RoutedEventArgs e) {
            this.timer.Stop();
            this.timer.Tick -= timer_Tick;
        }
    }
}
