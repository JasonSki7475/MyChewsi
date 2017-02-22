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
        private string _token;
        private bool _useProxy;
        private string _proxyAddress;
        private int _proxyPort;
        private string _proxyUserName;
        private string _proxyPassword;
        private const string Url = "http://chewsi-dev.azurewebsites.net/TXApi/"; //TODO https://www.chewsidental.com/TXAPI/
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
        /// <param name="providerAddress"></param>
        /// <param name="subscriber">The subscriber.</param>
        /// <returns>Chewsi Provider ID</returns>
        public ValidateSubscriberAndProviderResponse ValidateSubscriberAndProvider(ProviderInformation provider, ProviderAddressInformation providerAddress, 
            SubscriberInformation subscriber)
        {
            return Post<ValidateSubscriberAndProviderResponse>(new ValidateSubscriberAndProviderRequest
            {
                TIN = provider.TIN,
                RenderingState = providerAddress.RenderingState,
                RenderingZip = providerAddress.RenderingZip,
                RenderingCity = providerAddress.RenderingCity,
                RenderingAddress = providerAddress.RenderingAddress,
                NPI = provider.NPI,
                SubscriberDOB = subscriber.SubscriberDateOfBirth.ToString("d"),
                SubscriberFirstName = subscriber.SubscriberFirstName,
                SubscriberLastName = subscriber.SubscriberLastName,
                ChewsiID = subscriber.Id
            },
            ValidateSubscriberAndProviderUri);
        }

        public void ProcessClaim(ProviderInformation provider, SubscriberInformation subscriber, List<ProcedureInformation> procedures)
        {
            Post<string>(new ProcessClaimRequest
            {
                TIN = provider.TIN,
                OfficeNbr = provider.OfficeNbr,
                ClaimLines = procedures,
                NPI = provider.NPI,
                // TODO PIN = ,
                ProviderID = provider.Id,
                SubscriberDOB = subscriber.SubscriberDateOfBirth.ToString("d"),
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

        public void Initialize(string token, bool useProxy, string proxyAddress, int proxyPort, string proxyUserName, string proxyPassword)
        {
            _token = token;
            _useProxy = useProxy;
            _proxyAddress = proxyAddress;
            _proxyPort = proxyPort;
            _proxyUserName = proxyUserName;
            _proxyPassword = proxyPassword;
        }

        private T Post<T>(object request, string uri) where T: class 
        {
            var url = new Uri(new Uri(Url, UriKind.Absolute), uri);
            var webRequest = WebRequest.Create(url) as HttpWebRequest;
            webRequest.Headers.Clear();
            webRequest.Headers.Add("x-chewsi-token", _token);
            webRequest.Accept = "application/json";
            webRequest.ContentType = "application/json";
            webRequest.Method = "POST";

            if (_useProxy)
            {
                var proxy = new WebProxy(_proxyAddress, _proxyPort)
                {
                    Credentials = new NetworkCredential(_proxyUserName, _proxyPassword)
                };
                webRequest.Proxy = proxy;
            }

            // TODO
            // webRequest.Proxy = new WebProxy("http://localhost:8888");

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
                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        // 204
                        return null;
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
