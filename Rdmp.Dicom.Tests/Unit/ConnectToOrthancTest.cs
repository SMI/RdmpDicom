using Dicom.Network;
using NUnit.Framework;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace Rdmp.Dicom.Tests.Unit
{
    internal class PublicPacsTest
    {
        private const string LocalAetTitle = "STORESCP";
        public const string RemoteAetTitle = "ORTHANC";


        [TestCase("www.dicomserver.co.uk", 104)]
        public void EchoTest(string host, int port)
        {
            var success = false;
            var client = new DicomClient(host,port,false,LocalAetTitle,RemoteAetTitle);
            client.AddRequestAsync(new DicomCEchoRequest
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
