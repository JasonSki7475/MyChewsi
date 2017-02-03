using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ChewsiApi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string Url = "https://www.chewsidental.com/PMSApi/";
        private const string ValidateSubscriberAndProviderUri = "ValidateSubscriberAndProvider()";
        private const string ProcessClaimUri = "ProcessClaim()";
        private const string RequestClaimProcessingStatusUri = "RequestClaimProcessingStatus()";
        private const string InitializeUri = "Init";

        /// <summary>
        /// Validates the subscriber and provider.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="subscriber">The subscriber.</param>
        /// <returns>Chewsi Provider ID</returns>
        public ValidateSubscriberAndProviderResponse ValidateSubscriberAndProvider(ProviderInformationRequest provider, SubscriberInformationRequest subscriber)
        {
            return Post<ValidateSubscriberAndProviderResponse>(new
            {
                provider = provider,
                subscriber = subscriber
            }, 
            ValidateSubscriberAndProviderUri);
        }

        /// <summary>
        /// Processes the claim.
        /// </summary>
        /// <returns>Claim Number</returns>
        public string ProcessClaim(ProviderInformationRequest provider, SubscriberInformationRequest subscriber, ProcedureInformationRequest procedure)
        {
            return Post<string>(new
            {
                provider = provider,
                subscriber = subscriber,
                procedure = procedure
            }, 
            ProcessClaimUri);
        }

        public void RequestClaimProcessingStatus(ProviderInformationRequest provider)
        {
            Post<string>(new
            {
                provider = provider
            }, 
            RequestClaimProcessingStatusUri);
        }

        public void Initialize(InitializeRequest request)
        {
            Post<string>(request, RequestClaimProcessingStatusUri);
        }

        private T Post<T>(object request, string uri)
        {
            var url = new Uri(new Uri(Url, UriKind.Absolute), uri);
            var webRequest = WebRequest.Create(url) as HttpWebRequest;
            webRequest.Headers.Clear();
            webRequest.Accept = "application/json";
            webRequest.ContentType = "application/json";
            webRequest.Method = "POST";

            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
            webRequest.ContentLength = data.Length;
            try
            {
                using (Stream post = webRequest.GetRequestStream())
                {
                    post.Write(data, 0, data.Length);
                }

                using (var response = webRequest.GetResponse() as HttpWebResponse)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var stream = response.GetResponseStream();
                        if (stream != null)
                        {
                            var reader = new StreamReader(stream);
                            return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                        }
                    }
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // 401
                        Logger.Error("Error 401. Uri={0}", uri);
                        throw new InvalidOperationException("Wrong user name or password");
                    }
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        // 404
                        Logger.Error("Error 404. Uri={0}", uri);
                        throw new InvalidOperationException("Resource not found");
                    }
                    Logger.Error("Unsupported status code {0}. Uri={1}", response.StatusCode, uri);
                    throw new InvalidOperationException(string.Format("Unsupported status code {0}", response.StatusCode));
                }
            }
            catch (WebException e)
            {
                Logger.Error("Cannot send error report", e);
                throw;
            }
        }
    }
}
