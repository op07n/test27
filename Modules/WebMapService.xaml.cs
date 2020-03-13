using DevExpress.Xpf.Map;

namespace MapDemo {
    public partial class WebMapService : MapDemoModule {
        public WebMapService() {
            InitializeComponent();
        }
        void OnResponseCapabilities(object sender, CapabilitiesResponsedEventArgs e) {
            lbWmsLayers.ItemsSource = e.Layers;
        }
    }
}
