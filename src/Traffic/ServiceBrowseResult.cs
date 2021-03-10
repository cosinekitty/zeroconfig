namespace CosineKitty.ZeroConfigWatcher
{
    public class ServiceBrowseResult
    {
        public readonly string Name;
        public readonly string ServiceType;

        public ServiceBrowseResult(string name, string serviceType)
        {
            Name = name;
            ServiceType = serviceType;
        }

        public override string ToString()
        {
            return $"[{Name}] {ServiceType}";
        }
    }
}
