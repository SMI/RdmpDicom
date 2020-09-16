using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dicom;
using Dicom.Log;
using Dicom.Network;
using NUnit.Framework;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace Rdmp.Dicom.Tests.Unit
{
    internal class PacsFetch
    {
        class QRService : DicomService, IDicomServiceProvider, IDicomCFindProvider, IDicomCEchoProvider,
            IDicomCMoveProvider
        {
            public QRService(INetworkStream stream, Encoding fallbackEncoding, Logger log) : base(stream, fallbackEncoding, log)
            {
            }

            public DicomCEchoResponse OnCEchoRequest(DicomCEchoRequest request)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<DicomCFindResponse> OnCFindRequest(DicomCFindRequest request)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<DicomCMoveResponse> OnCMoveRequest(DicomCMoveRequest request)
            {
                throw new NotImplementedException();
            }

            public void OnConnectionClosed(Exception exception)
            {
                throw new NotImplementedException();
            }

            public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
            {
                throw new NotImplementedException();
            }

            public Task OnReceiveAssociationReleaseRequestAsync() => SendAssociationReleaseResponseAsync();

            public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
            {
                foreach (var pc in association.PresentationContexts)
                {
                    if (pc.AbstractSyntax == DicomUID.Verification
                    || pc.AbstractSyntax==DicomUID.PatientRootQueryRetrieveInformationModelFIND
                    || pc.AbstractSyntax==DicomUID.PatientRootQueryRetrieveInformationModelMOVE
                    || pc.AbstractSyntax==DicomUID.StudyRootQueryRetrieveInformationModelFIND
                    || pc.AbstractSyntax==DicomUID.StudyRootQueryRetrieveInformationModelMOVE)
                    {
                        pc.AcceptTransferSyntaxes(DicomTransferSyntax.ExplicitVRLittleEndian);
                    } else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                    {
                        pc.AcceptTransferSyntaxes();
                    }
                    else
                    {
                        pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
                    }
                }
                return SendAssociationAcceptAsync(association);
            }
        }

        private IDicomServer _ourPacs;

        [OneTimeSetUp]
        public void StartOwnPacs()
        {
            _ourPacs=DicomServer.Create<QRService>(11112);
        }

        [OneTimeTearDown]
        public void StopOwnPacs()
        {
            _ourPacs.Stop();
        }

        [Test]
        public void EchoTest()
        {
            var success=false;
            var client = new DicomClient("localhost", 11112, false, "me", "also_me");
            client.AddRequestAsync(new DicomCEchoRequest
                {
                    OnResponseReceived = (req, res) => {
                        success = true;
                    }
                }
            ).Wait();
            client.SendAsync().Wait();
            Assert.True(success, "No echo response from own PACS");
        }
        /*
        [Test]
        public void RetryFillTest()
        {
            var target = new[]
            {
                new Item("patientId","studyUid","seriesUid","sopInstanceUid1"),
                new Item("patientId","studyUid","seriesUid","sopInstanceUid2")
            };
            var hbo=new HierarchyBasedOrder(new DateTime(2020,1,1),new DateTime(2020,12,31),PlacementMode.PlaceThenFill,OrderLevel.Study,new ThrowImmediatelyDataLoadEventListener());
            hbo.Place(target[0]);
            hbo.Place(target[1]);

            var picker=hbo.NextPicker();
            picker.GetDicomCMoveRequest("dummy",out var retryCount);
            Assert.AreEqual(1, retryCount);

            // Simulate error+retry on *second* item in study
            picker.Fill(target[0]);
            hbo.Retry(target[1]);
            picker = hbo.NextPicker();

            picker.GetDicomCMoveRequest("dummy", out retryCount);
            Assert.AreEqual(1, retryCount);
            
            Assert.False(picker.IsFilled());
            picker.Fill(target[0]);
            picker.Fill(target[1]);
            Assert.True(picker.IsFilled());
        }*/
    }
}
