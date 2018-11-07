using System;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Auth
{
    [DataContract]
    public class TraktAuthDeviceCodeToken
    {
        [DataMember(Name = "device_code")]
        public string DeviceCode { get; set; }

        [DataMember(Name = "user_code")]
        public string UserCode { get; set; }

        [DataMember(Name = "verification_url")]
        public string VerificationUrl { get; set; }

        [DataMember(Name = "expires_in")]
        public int ExpiresIn { get; set; }

        [DataMember(Name = "interval")]
        public int Interval { get; set; }

        public DateTime RequestTime;

        public TraktAuthDeviceCodeToken()
        {
            RequestTime = DateTime.Now;
        }
    }
}