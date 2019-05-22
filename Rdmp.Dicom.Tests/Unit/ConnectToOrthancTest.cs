using Dicom.Network;
using NUnit.Framework;

namespace Rdmp.Dicom.Tests.Unit
{
    [Ignore("Requires orthanc dicom server to be running")]
    class OrthancTest
    {
        private const string LocalAetTitle = "STORESCP";
        public const string RemoteAetTitle = "ORTHANC";

        [Test]
        public void TestCreatingConnectionToLocalhostByIP()
        {
            var stream = DesktopNetworkManager.CreateNetworkStream("127.0.0.1", 4242, false, true, true);
        }
        [Test]
        public void TestCreatingConnectionToLocalhostByHostName()
        {
            var stream = DesktopNetworkManager.CreateNetworkStream("localhost", 4242, false, true, true);
        }


        [Test]
        public void EchoOrthancTest()
        {
            var stream = DesktopNetworkManager.CreateNetworkStream("localhost", 4242, false, true, true);
            
            var client = new DicomClient();
            client.AddRequest(new DicomCEchoRequest());
            
            var sendTask = client.SendAsync(stream,
                    LocalAetTitle,
                RemoteAetTitle);

            sendTask.Wait(1000);

            Assert.IsTrue(sendTask.IsCompleted);
            Assert.IsFalse(sendTask.IsFaulted);
            Assert.IsNull(sendTask.Exception);

        }

        
    }
}
