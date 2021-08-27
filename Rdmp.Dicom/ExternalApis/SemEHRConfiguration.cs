using Rdmp.Core.Curation.Data.Aggregation;
using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Rdmp.Dicom.ExternalApis
{
    public class SemEHRConfiguration
    {
        public string Url { get; set; } = "http://127.0.0.1:80/";
        public List<string> Terms { get; set; }  = new List<string>();

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
            // if Url is defined in the Catalogue then we should use that value
            if(!string.IsNullOrWhiteSpace(over.Url))
            {
                Url = over.Url;
            }

            // TODO: For terms do you want Catalogue ones + Aggregate Set ones or just to use the Aggregate Set ones

            return this;
        }
    }
}
