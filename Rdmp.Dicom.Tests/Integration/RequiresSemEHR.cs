using System;
using System.IO;
using System.Net.Http;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using YamlDotNet.Serialization;
using System.Threading;
using System.Net;

namespace Rdmp.Dicom.Tests.Integration
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface |
                    AttributeTargets.Assembly, AllowMultiple = true)]
    public class RequiresSemEHR : Attribute, IApplyToContext
    {
        static HttpClient httpClient = new HttpClient();
        public const string SemEHRTestUrl = "https://localhost:8485";

        public void ApplyToContext(TestExecutionContext context)
        {
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            try
            {
                HttpResponseMessage response = httpClient.GetAsync(SemEHRTestUrl, cts.Token).Result;

                //Check the status code is 200 success
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Assert.Ignore($"SemEHR did not respond correctly on {SemEHRTestUrl}: {response.StatusCode}");
                }
            }
            catch (Exception)
            {
                Assert.Ignore($"SemEHR not running on {SemEHRTestUrl}");
            }
            finally
            {
                if (cts != null)
                {
                    cts.Dispose();
                }
            }
        }
    }
}
