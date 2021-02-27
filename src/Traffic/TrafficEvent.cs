namespace CosineKitty.ZeroConfigWatcher
{
    public delegate void TrafficEventHandler(object sender, TrafficEventArgs e);

    public class TrafficEventArgs
    {
        public byte[] RawData;
    }
}