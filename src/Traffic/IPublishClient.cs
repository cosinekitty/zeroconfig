
namespace CosineKitty.ZeroConfigWatcher
{
    public interface IPublishClient
    {
        void OnPublish(string requestedName, string actualName);
    }
}
