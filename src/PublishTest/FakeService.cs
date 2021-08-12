using System;
using CosineKitty.ZeroConfigWatcher;

namespace PublishTest
{
    internal class FakeService : IPublishClient
    {
        public void OnPublish(string requestedName, string actualName)
        {
            Console.WriteLine($"FakeService.OnPublish: requested name = [{requestedName}], actual name = [{actualName}]");
        }
    }
}
