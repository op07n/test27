using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using DevExpress.Map;
using DevExpress.Mvvm;
using DevExpress.Xpf.Map;

namespace MapDemo {
    public partial class MapElements : MapDemoModule {
        FlightMapDataGenerator dataGenerator;

        public MapElements() {
            InitializeComponent();
            dataGenerator = new FlightMapDataGenerator(Resources["airportTemplate"] as DataTemplate, Resources["planeTemplate"] as DataTemplate, planeInfoPanel);
            DataContext = dataGenerator;
            dataGenerator.SpeedScale = Convert.ToDouble(tbSpeedScale.Value);
            ModuleUnloaded += MapElements_Unloaded;
        }

        void MapElements_Unloaded(object sender, RoutedEventArgs e) {
            dataGenerator.Dispose();
        }
        void tbSpeedScale_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e) {
            if(dataGenerator != null)
                dataGenerator.SpeedScale = Convert.ToDouble(e.NewValue);
        }
    }

    public class PlaneTrajectory {
        class TrajectoryPart {
            readonly GeoPoint startPointField;
            readonly GeoPoint endPointField;
            readonly double flightTimeField;
            readonly double courseField;

            public GeoPoint StartPoint { get { return startPointField; } }
            public GeoPoint EndPoint { get { return endPointField; } }
            public double FlightTime { get { return flightTimeField; } }
            public double Course { get { return courseField; } }

            public TrajectoryPart(ProjectionBase projection, GeoPoint startPoint, GeoPoint endPoint, double speedInKmH) {
                this.startPointField = startPoint;
                this.endPointField = endPoint;
                Size sizeInKm = projection.GeoToKilometersSize(startPoint, new Size(Math.Abs(startPoint.Longitude - endPoint.Longitude), Math.Abs(startPoint.Latitude - endPoint.Latitude)));
                double partlength = Math.Sqrt(sizeInKm.Width * sizeInKm.Width + sizeInKm.Height * sizeInKm.Height);
                flightTimeField = partlength / speedInKmH;
                courseField = Math.Atan2((endPoint.Longitude - startPoint.Longitude), (endPoint.Latitude - startPoint.Latitude)) * 180 / Math.PI;
            }
            public GeoPoint GetPointByCurrentFlightTime(double currentFlightTime, ProjectionBase projection) {
                if(currentFlightTime > FlightTime)
                    return endPointField;
                double ratio = currentFlightTime / FlightTime;
                return new GeoPoint(startPointField.Latitude + ratio * (endPointField.Latitude - startPointField.Latitude), startPointField.Longitude + ratio * (endPointField.Longitude - startPointField.Longitude));
            }
        }

        readonly List<TrajectoryPart> trajectory = new List<TrajectoryPart>();
        readonly SphericalMercatorProjection projection = new SphericalMercatorProjection();

        public GeoPoint StartPoint {
            get { return (trajectory.Count > 0) ? trajectory[0].StartPoint : new GeoPoint(0, 0); }
        }
        public GeoPoint EndPoint {
            get { return (trajectory.Count > 0) ? trajectory[trajectory.Count - 1].EndPoint : new GeoPoint(0, 0); }
        }
        public double FlightTime {
            get {
                double result = 0.0;
                foreach(TrajectoryPart part in trajectory)
                    result += part.FlightTime;
                return result;
            }
        }

        public PlaneTrajectory(List<GeoPoint> points, double speedInKmH) {
            for(int i = 0; i < points.Count - 1; i++)
                trajectory.Add(new TrajectoryPart(projection, points[i], points[i + 1], speedInKmH));
        }
        public GeoPoint GetPointByCurrentFlightTime(double currentFlightTime) {
            SphericalMercatorProjection projection = new SphericalMercatorProjection();
            double time = 0.0;
            for(int i = 0; i < trajectory.Count - 1; i++) {
                if(trajectory[i].FlightTime > currentFlightTime - time)
                    return trajectory[i].GetPointByCurrentFlightTime(currentFlightTime - time, projection);
                time += trajectory[i].FlightTime;
            }
            return trajectory[trajectory.Count - 1].GetPointByCurrentFlightTime(currentFlightTime - time, projection);
        }
        public CoordPointCollection GetAirPath() {
            CoordPointCollection result = new CoordPointCollection();
            foreach(TrajectoryPart trajectoryPart in trajectory)
                result.Add(trajectoryPart.StartPoint);
            if(trajectory.Count > 0)
                result.Add(trajectory[trajectory.Count - 1].EndPoint);
            return result;
        }
        public double GetCourseByCurrentFlightTime(double currentFlightTime) {
            double time = 0.0;
            for(int i = 0; i < trajectory.Count - 1; i++) {
                if(trajectory[i].FlightTime > currentFlightTime - time)
                    return trajectory[i].Course;
                time += trajectory[i].FlightTime;
            }
            return trajectory[trajectory.Count - 1].Course;
        }
        public void UpdateTrajectory(List<CoordPoint> points, double speedInKmH) {
            trajectory.Clear();
            for(int i = 0; i < points.Count - 1; i++)
                trajectory.Add(new TrajectoryPart(projection, (GeoPoint)points[i], (GeoPoint)points[i + 1], speedInKmH));
        }
    }

    public class PlaneInfo : BindableBase {
        GeoPoint position = new GeoPoint(0,0);
        public PlaneInfo() {
            CurrentFlightTime = 0;
            Course = 0;
        }

        public double CurrentFlightTime {
            get { return GetProperty(() => CurrentFlightTime); }
            set { SetProperty(() => CurrentFlightTime, value, CurrentFlightTimePropertyChanged); }
        }
        public GeoPoint Position {
            get { return GetProperty(() => position); }
            set { SetProperty(() => position, value); }
        }
        public double Course {
            get { return GetProperty(() => Course); }
            set { SetProperty(() => Course, value); }
        }

        void CurrentFlightTimePropertyChanged() {
            this.UpdatePosition(CurrentFlightTime);
        }
        static string ConvertPlaneNameToFilePath(string PlaneName) {
            string result = PlaneName.Replace(" ", "");
            result = "../Images/Planes/" + result.Replace("-", "") + ".png";
            return result;
        }

        bool isLandedField = false;
        readonly string planeIDField;
        readonly string nameField;
        readonly string endPointNameField;
        readonly string startPointNameField;
        readonly double speedInKmHField;
        readonly double flightAltitudeField;
        readonly string imagePathField;
        readonly PlaneTrajectory trajectoryField;

        public string PlaneID { get { return planeIDField; } }
        public string Name { get { return nameField; } }
        public string EndPointName { get { return endPointNameField; } }
        public string StartPointName { get { return startPointNameField; } }
        public double SpeedKmH { get { return isLandedField ? 0.0 : speedInKmHField; } }
        public double FlightAltitude { get { return isLandedField ? 0.0 : flightAltitudeField; } }
        public string ImagePath { get { return imagePathField; } }
        public bool IsLanded { get { return isLandedField; } }
        public double TotalFlightTime { get { return trajectoryField.FlightTime; } }

        public PlaneInfo(string name, string id, string endPointName, string startPointName, double speedInKmH, double flightAltitude, List<GeoPoint> points) {
            this.nameField = name;
            this.planeIDField = id;
            this.endPointNameField = endPointName;
            this.startPointNameField = startPointName;
            this.speedInKmHField = speedInKmH;
            this.flightAltitudeField = flightAltitude;
            imagePathField = ConvertPlaneNameToFilePath(name);
            trajectoryField = new PlaneTrajectory(points, speedInKmH);
            UpdatePosition(CurrentFlightTime);
        }
        void UpdatePosition(double flightTime) {
            GeoPoint pos = trajectoryField.GetPointByCurrentFlightTime(flightTime);
            double course = trajectoryField.GetCourseByCurrentFlightTime(flightTime);
            isLandedField = flightTime >= trajectoryField.FlightTime;
            Position = pos;
            Course = course;
        }
        public List<MapItem> GetAirPath(DataTemplate airportTemplate) {
            List<MapItem> mapItemList = new List<MapItem>();
            MapPolyline polyline = new MapPolyline() { Points = trajectoryField.GetAirPath(), Fill = new SolidColorBrush(Colors.Transparent), Stroke = new SolidColorBrush(Color.FromArgb(127, 255, 0, 199)), StrokeStyle = new StrokeStyle() { Thickness = 4 }, IsGeodesic = true, Tag = this };
            trajectoryField.UpdateTrajectory(polyline.ActualPoints.ToList(), SpeedKmH);
            mapItemList.Add(polyline);
            mapItemList.Add(new MapCustomElement() { Location = trajectoryField.StartPoint, ContentTemplate = airportTemplate, Tag = this });
            mapItemList.Add(new MapCustomElement() { Location = trajectoryField.EndPoint, ContentTemplate = airportTemplate, Tag = this });
            return mapItemList;
        }
    }

    public class FlightMapDataGenerator : BindableBase, IDisposable {
        ObservableCollection<MapCustomElement> planes = new ObservableCollection<MapCustomElement>();
        ObservableCollection<MapItem> actualAirPaths = new ObservableCollection<MapItem>();
        PlaneInfo selectedPlaneInfo = new PlaneInfo();
        public ObservableCollection<MapCustomElement> Planes {
            get { return GetProperty(() => planes); }
            set { SetProperty(() => planes, value); }
        }
        public ObservableCollection<MapItem> ActualAirPaths {
            get { return GetProperty(() => actualAirPaths); }
            set { SetProperty(() => actualAirPaths, value); }
        }
        public PlaneInfo SelectedPlaneInfo {
            get { return GetProperty(() => selectedPlaneInfo); }
            set { SetProperty(() => selectedPlaneInfo, value, SelectedPlaneProperyChanged); }
        }
        public double SpeedScale {
            get { return GetProperty(() => SpeedScale); }
            set { SetProperty(() => SpeedScale, value); }
        }

        void SelectedPlaneProperyChanged() {
            this.UpdatePlaneInfo();
        }

        const double mSecPerHour = 3600000;

        readonly DispatcherTimer timer = new DispatcherTimer();
        readonly DataTemplate airportTemplate;
        readonly List<PlaneInfo> planesInfo = new List<PlaneInfo>();
        readonly PlaneInfoPanel infoPanel;
        DateTime lastTime;

        public FlightMapDataGenerator(DataTemplate airportTemplate, DataTemplate planeTemplate, PlaneInfoPanel infoPanel) {
            Planes = new ObservableCollection<MapCustomElement>();
            ActualAirPaths = new ObservableCollection<MapItem>();
            this.airportTemplate = airportTemplate;
            this.infoPanel = infoPanel;
            LoadFromXML(planeTemplate);
            timer.Tick += new EventHandler(OnTimedEvent);
            timer.Interval = new TimeSpan(0, 0, 2);
            lastTime = DateTime.Now;
            timer.Start();
            if(Planes != null)
                SelectedPlaneInfo = Planes[1].Content as PlaneInfo;
        }
        void LoadFromXML(DataTemplate planeTemplate) {
            XDocument document = DataLoader.LoadXmlFromResources("/Data/FlightMap.xml");
            if(document != null) {
                foreach(XElement element in document.Element("Planes").Elements()) {
                    List<GeoPoint> points = new List<GeoPoint>();
                    foreach(XElement infoElement in element.Element("Path").Elements()) {
                        GeoPoint geoPoint = new GeoPoint(Convert.ToDouble(infoElement.Element("Latitude").Value, CultureInfo.InvariantCulture), Convert.ToDouble(infoElement.Element("Longitude").Value, CultureInfo.InvariantCulture));
                        points.Add(geoPoint);
                    }
                    PlaneInfo info = new PlaneInfo(element.Element("PlaneName").Value, element.Element("PlaneID").Value, element.Element("EndPointName").Value, element.Element("StartPointName").Value, Convert.ToInt32(element.Element("Speed").Value), Convert.ToInt32(element.Element("Altitude").Value), points);
                    info.CurrentFlightTime = Convert.ToDouble(element.Element("CurrentFlightTime").Value, CultureInfo.InvariantCulture);
                    planesInfo.Add(info);
                }
            }
            foreach(PlaneInfo info in planesInfo) {
                MapCustomElement mapCustomElement = new MapCustomElement() { Content = info, Tag = info, ContentTemplate = planeTemplate };
                BindingOperations.SetBinding(mapCustomElement, MapCustomElement.LocationProperty, new Binding("Position") { Source = info });
                Planes.Add(mapCustomElement);
                AddPaths(info);
            }
        }
        void AddPaths(PlaneInfo planeInfo) {
            if(planeInfo != null)
                foreach(MapItem item in planeInfo.GetAirPath(airportTemplate)) {
                    BindingOperations.SetBinding(item, MapItem.VisibleProperty, new Binding("SelectedPlaneInfo") {
                        Source = this, Converter = new PlaneInfoToPathVisibilityConverter(), ConverterParameter = item.Tag
                    });
                    ActualAirPaths.Add(item);
                }
        }
        void UpdatePlaneInfo() {
            infoPanel.Visible = SelectedPlaneInfo != null;
        }
        void OnTimedEvent(object source, EventArgs e) {
            DateTime currentTime = DateTime.Now;
            TimeSpan interval = currentTime.Subtract(lastTime);
            foreach(PlaneInfo info in planesInfo) {
                if(!info.IsLanded)
                    info.CurrentFlightTime += SpeedScale * interval.TotalMilliseconds / mSecPerHour;
            }
            lastTime = currentTime;
        }
        public void Dispose() {
            timer.Stop();
            timer.Tick -= OnTimedEvent;
        }
    }
}
