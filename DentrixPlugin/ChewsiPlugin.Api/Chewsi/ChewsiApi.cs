using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ChewsiApi : IChewsiApi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string Url = "https://chewsi-dev.azurewebsites.net/TXApi/"; //TODO https://www.chewsidental.com/TXAPI/
        private const string ValidateSubscriberAndProviderUri = "ValidateSubscriberAndProvider";
        private const string ProcessClaimUri = "ProcessClaim";
        private const string RegisterPluginUri = "RegisterPlugin";
        private const string Request835DownloadsUri = "Request835Downloads";
        private const string RequestClaimProcessingStatusUri = "RequestClaimProcessingStatus";
        private const string ReceiveMemberAuthorizationUri = "ReceiveMemberAuthorization";
        private const string DownoadFileRequestUri = "DownoadFileRequest";
        private const string UpdatePluginRegistrationUri = "UpdatePluginRegistration";

        /// <summary>
        /// Validates the subscriber and provider.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="subscriber">The subscriber.</param>
        /// <returns>Chewsi Provider ID</returns>
        public ValidateSubscriberAndProviderResponse ValidateSubscriberAndProvider(ProviderInformation provider, SubscriberInformation subscriber)
        {
            return Post<ValidateSubscriberAndProviderResponse>(new ValidateSubscriberAndProviderRequest
            {
                TIN = provider.TIN,
                RenderingState = provider.RenderingState,
                RenderingZip = provider.RenderingZip,
                RenderingCity = provider.RenderingCity,
                RenderingAddress = provider.RenderingAddress,
                NPI = provider.NPI,
                SubscriberDOB = subscriber.SubscriberDateOfBirth,
                SubscriberFirstName = subscriber.SubscriberFirstName,
                SubscriberLastName = subscriber.SubscriberLastName,
                // TODO ChewsiID = 
            },
            ValidateSubscriberAndProviderUri);
        }

        public void ProcessClaim(ProviderInformation provider, SubscriberInformation subscriber, List<ProcedureInformation> procedures)
        {
            Post<string>(new ProcessClaimRequest
            {
                TIN = provider.TIN,
                RenderingState = provider.RenderingState,
                RenderingZip = provider.RenderingZip,
                RenderingCity = provider.RenderingCity,
                RenderingAddress = provider.RenderingAddress,
                ClaimLines = procedures,
                NPI = provider.NPI,
                // TODO PIN = ,
                ProviderID = provider.Id,
                SubscriberDOB = subscriber.SubscriberDateOfBirth,
                SubscriberFirstName = subscriber.SubscriberFirstName,
                SubscriberID = subscriber.Id,
                SubscriberLastName = subscriber.SubscriberLastName
            },
            ProcessClaimUri);
        }

        /// <summary>
        /// Registers the plugin. Returns machine Id.
        /// </summary>
        public string RegisterPlugin(RegisterPluginRequest request)
        {
            var response = Post<RegisterPluginResponse>(request, RegisterPluginUri);
            return response?.Token;
        }

        public void UpdatePluginRegistration(UpdatePluginRegistrationRequest request)
        {
            Post<string>(request, UpdatePluginRegistrationUri);
        }

        public Request835DownloadsResponse Get835Downloads(Request835Downloads request)
        {
            return Post<Request835DownloadsResponse>(request, Request835DownloadsUri);
        }

        public void ReceiveMemberAuthorization(ReceiveMemberAuthorizationRequest request)
        {
            Post<string>(request, ReceiveMemberAuthorizationUri);
        }

        public ClaimProcessingStatusResponse GetClaimProcessingStatus(ClaimProcessingStatusRequest request)
        {
            return Post<ClaimProcessingStatusResponse>(request, RequestClaimProcessingStatusUri);
        }

        public string DownoadFile(DownoadFileRequest request)
        {
            return Post<string>(request, DownoadFileRequestUri);
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
