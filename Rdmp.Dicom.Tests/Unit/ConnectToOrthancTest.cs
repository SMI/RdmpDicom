using System.Diagnostics;
using Dicom.Network;
using NUnit.Framework;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace Rdmp.Dicom.Tests.Unit
{
    [Ignore("Requires orthanc dicom server to be running")]
    class OrthancTest
    {
        private const string LocalAetTitle = "STORESCP";
        public const string RemoteAetTitle = "ORTHANC";


        [TestCase("127.0.0.1", 4242)]
        [TestCase("localhost", 4242)]
        public void EchoOrthancTest(string host, int port)
        {
            bool success = false;
            var stream = DesktopNetworkManager.CreateNetworkStream("localhost", 4242, false, true, true);
            
            var client = new DicomClient(host,port,false,LocalAetTitle,RemoteAetTitle);
            client.AddRequestAsync(new DicomCEchoRequest()
            {
                OnResponseReceived = (req,res) => {
                    success = true;
                }
            }
            ).Wait();
            client.SendAsync().Wait();
            Assert.True(success,$"No echo response from PACS on {host}:{port}");
        }

        
    }
}
