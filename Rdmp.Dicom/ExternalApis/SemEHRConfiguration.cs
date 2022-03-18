using Rdmp.Core.Curation.Data.Aggregation;
using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Rdmp.Dicom.ExternalApis
{
    public class SemEHRConfiguration
    {
        [YamlIgnore]
        public Dictionary<string, string> TemporalityOptions = new()
        {
             {"Recent", "Recent" },
            { "Historical", "Historical" },
            { "Hypothetical", "Hypothetical" }
        };

        [YamlIgnore]
        public Dictionary<string, string> NegationOptions = new()
        {
            { "Any", "Any" },
            { "Negated - The query term is mentioned in terms of absence.", "Negated" },
            { "Affirmed - The query term is confirmed to be present.", "Affirmed" }
        };

        [YamlIgnore]
        public Dictionary<string, string> ModalityOptions = new()
        {
            { "CR - Computed Radiography", "CR" },
            { "CT - Computed Tomography", "CT" },
            { "DX - Digital Radiography", "DX" },
            { "MG - Mammography", "MG" },
            { "MR - Magnetic Resonance", "MR" },
            { "NM - Nuclear Medicine", "NM" },
            { "OT - Other", "OT" },
            { "PR - Presentation State", "PR" },
            { "PT - Positron emission tomography (PET)", "PT" },
            { "RF - Radio Fluoroscopy", "RF" },
            { "US - Ultrasound", "US" },
            { "XA - X-Ray Angiography", "XA" }
        };

        [YamlIgnore]
        public Dictionary<string, string> ReturnFieldOptions = new()
        {
            { "SOPInstanceUID", "SOPInstanceUID" },
            { "SeriesInstanceUID", "SeriesInstanceUID" },
            { "StudyInstanceUID", "StudyInstanceUID" }
        };

        //API Settings
        /// <summary>
        /// The URL used to connect to the API
        /// </summary>
        public string Url { get; set; } = "https://192.168.1.200:8485/api/search_anns/myQuery/";

        /// <summary>
        /// TRUE if the <see cref="Url">Url</see> fir tge API should check that the certificate and certificate chain are valid
        /// </summary>
        public bool ValidateServerCert { get; set; } = true;

        /// <summary>
        /// The passphrase required to connect to the API
        /// </summary>
        public string Passphrase { get; set; } = "";

        /// <summary>
        /// The date format for the API start date and end date filter
        /// </summary>
        public string StartEndDateFormat { get; set; } = "yyyy-MM-dd";

        //API Search
        /// <summary>
        /// Free text or comma-separated list of CUIs
        /// </summary>
        public string Query { get; set; } = "";

        /// <summary>
        /// Limit the query expansion to depth N if searching CUIs
        /// </summary>
        public int QDepth { get; set; } = -1;

        /// <summary>
        /// Remove CUIs from the expanded list (e.g. C12345)
        /// </summary>
        public string QStop { get; set; } = "";

        /// <summary>
        /// Whether the comment was made regarding "Recent" or "historical" or "hypothetical"
        /// </summary>
        public List<string> Temporality { get; set; } = new();

        /// <summary>
        /// Whether the comment was a confrimation or negation of the presence/absence of the search term - "Any" or "Negated" or "Affirmed"
        /// </summary>
        public string Negation { get; set; } = "";

        /// <summary>
        /// Who experianced the condition - "Patient" or "Other"
        /// </summary>
        public string Experiencer { get; set; } = "";

        //API Filter
        /// <summary>
        /// Determines whether to use the start date in the filter
        /// </summary>
        public bool UseStartDate { get; set; } = false;

        /// <summary>
        /// Filter the returned values to only include those FROM this date
        /// </summary>
        public DateTime StartDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Determines whether to use the end date in the filter
        /// </summary>
        public bool UseEndDate { get; set; } = false;

        /// <summary>
        /// Filter the returned values to only include those TO this date
        /// </summary>
        public DateTime EndDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Filter to only include specific modalities - e.g. "CT", "MR", "US", "PT", "CR", "OT", "XA", "RF", "DX", "MG", "PR", "NM"
        /// </summary>
        public List<string> Modalities { get; set; } = new();

        //API Return Fields
        /// <summary>
        /// The list of fields that should be returned - "SOPInstanceUID", "SeriesInstanceUID", "StudyInstanceUID" 
        /// </summary>
        //Currently only supporting one return feild which is all we need from an RDMP point of view
        //public List<string> ReturnFields { get; set; } = new List<string>();

        //API Return Field
        /// <summary>
        /// The field that should be returned - "SOPInstanceUID", "SeriesInstanceUID", "StudyInstanceUID" 
        /// </summary>
        public string ReturnField { get; set; } = "";

        public static SemEHRConfiguration LoadFrom(AggregateConfiguration aggregate)
        {
            LoadFrom(aggregate, out SemEHRConfiguration main, out SemEHRConfiguration over);

            if(main != null && over != null)
            {
                return main.OverrideWith(over);
            }

            return over ?? main ?? new SemEHRConfiguration();
        }

        public static void LoadFrom(AggregateConfiguration ac, out SemEHRConfiguration main, out SemEHRConfiguration over)
        {
            string mainYaml = ac.Catalogue.Description;
            string overrideYaml = ac.Description;

            Deserializer d = new Deserializer();

            main = string.IsNullOrWhiteSpace(mainYaml) ? null : d.Deserialize<SemEHRConfiguration>(mainYaml);
            over = string.IsNullOrWhiteSpace(overrideYaml) ? null : d.Deserialize<SemEHRConfiguration>(overrideYaml);
        }

        public string Serialize()
        {
            var s = new Serializer();
            return s.Serialize(this);
        }

        /// <summary>
        /// Overrides values in the current instance with the values in <paramref name="over"/>
        /// </summary>
        /// <param name="over"></param>
        /// <returns></returns>
        private SemEHRConfiguration OverrideWith(SemEHRConfiguration over)
        {
            // The settings/values are defined in the Catalogue then we should use that value

            // API Settings
            if (!string.IsNullOrWhiteSpace(over.Url))
            {
                Url = over.Url;
            }
            ValidateServerCert = over.ValidateServerCert;
            if (!string.IsNullOrWhiteSpace(over.Passphrase))
            {
                Passphrase = over.Passphrase;
            }
            if (!string.IsNullOrWhiteSpace(over.StartEndDateFormat))
            {
                StartEndDateFormat = over.StartEndDateFormat;
            }

            //API Query
            if (!string.IsNullOrWhiteSpace(over.Query))
            {
                Query = over.Query;
            }
            if (over.QDepth > -1)
            {
                QDepth = over.QDepth;
            }
            if (!string.IsNullOrWhiteSpace(over.QStop))
            {
                QStop = over.QStop;
            }
            if (!string.IsNullOrWhiteSpace(over.Negation))
            {
                Negation = over.Negation;
            }
            if (!string.IsNullOrWhiteSpace(over.Experiencer))
            {
                Experiencer = over.Experiencer;
            }
            if (over.Temporality.Count > 0)
            {
                Temporality = over.Temporality;
            }
            UseStartDate = over.UseStartDate;
            StartDate = over.StartDate;
            UseEndDate = over.UseEndDate;
            EndDate = over.EndDate;
            if (over.Modalities.Count > 0)
            {
                Modalities = over.Modalities;
            }
            /*if (over.ReturnFields.Count > 0)
            {
                ReturnFields = over.ReturnFields;
            }*/
            if (!string.IsNullOrWhiteSpace(over.ReturnField))
            {
                ReturnField = over.ReturnField;
            }

            // TODO: For terms do you want Catalogue ones + Aggregate Set ones or just to use the Aggregate Set ones
            return this;
        }

        public JObject GetQueryJson()
        {           
            //Set the terms
            dynamic termsObj = new JObject();
            if (!string.IsNullOrWhiteSpace(Query))
                termsObj.q = HttpUtility.UrlEncode(Query);
            if(QDepth > -1)
                termsObj.qdepth = QDepth;
            if(!string.IsNullOrWhiteSpace(QStop))
                termsObj.qstop = QStop; 
            if (!string.IsNullOrWhiteSpace(Negation))
                termsObj.negation = Negation;
            if (!string.IsNullOrWhiteSpace(Experiencer))
                termsObj.experiencer = Experiencer;
            if (Temporality.Count > 0)
                termsObj.temporality = new JArray(Temporality);

            //Add terms to terms array
            JArray termsArray = new JArray();
            termsArray.Add(termsObj);

            //Set the filter
            dynamic filterObj = new JObject();
            if (UseStartDate)
                filterObj.start_date = StartDate.ToString(StartEndDateFormat);
            if (UseEndDate)
                filterObj.end_date = EndDate.ToString(StartEndDateFormat);
            if (Modalities.Count > 0)
                filterObj.modalities = new JArray(Modalities);

            //Create API JSON
            dynamic apiCallJson = new JObject();
            apiCallJson.terms = termsArray;
            apiCallJson.filter = filterObj;
            /*if (ReturnFields.Count > 0)
                apiCallJson.returnFields = new JArray(ReturnFields);*/
            if (!string.IsNullOrWhiteSpace(ReturnField))
                apiCallJson.returnFields = new JArray(ReturnField);

            return apiCallJson;
        }

        public string GetQueryJsonAsString()
        {
            return (Regex.Replace(GetQueryJson().ToString(), @"\s+", ""));
            
        }

        public string GetUrlWithQuerystring()
        {
            string fullRequestUrl = Url;
            string queryJsonForUrl = "j=" + GetQueryJsonAsString();

            if (!string.IsNullOrEmpty(Passphrase))
            {
                fullRequestUrl += "?passphrase=" + Passphrase + "&" + queryJsonForUrl;
            }
            else
            {
                fullRequestUrl += "?" + queryJsonForUrl;
            }
            return (fullRequestUrl);
        }
    }
}
