using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using ChewsiPlugin.Api.Interfaces;
using Newtonsoft.Json;
using NLog;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ChewsiApi : IChewsiApi
    {
        private readonly IDialogService _dialogService;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _token;
        private bool _useProxy;
        private string _proxyAddress;
        private int _proxyPort;
        private string _proxyUserName;
        private string _proxyPassword;
        private const string Url = "http://chewsi-dev-txapi.azurewebsites.net";//"http://chewsi-dev.azurewebsites.net/TXApi/"; //TODO https://www.chewsidental.com/TXAPI/
        private const string ValidateSubscriberAndProviderUri = "ValidateSubscriberAndProvider";
        private const string ProcessClaimUri = "ProcessClaim";
        private const string RegisterPluginUri = "RegisterPlugin";
        private const string Request835DownloadsUri = "Request835Downloads";
        private const string RequestClaimProcessingStatusUri = "RequestClaimProcessingStatus";
        private const string ReceiveMemberAuthorizationUri = "ReceiveMemberAuthorization";
        private const string UpdatePluginRegistrationUri = "UpdatePluginRegistration";

        public ChewsiApi(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

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
                RenderingAddress1 = providerAddress.RenderingAddress1,
                RenderingAddress2 = providerAddress.RenderingAddress2,
                NPI = provider.NPI,
                SubscriberDOB = subscriber.SubscriberDateOfBirth.ToString("d"),
                SubscriberFirstName = subscriber.SubscriberFirstName,
                SubscriberLastName = subscriber.SubscriberLastName,
                ChewsiID = subscriber.Id ?? ""
            },
                ValidateSubscriberAndProviderUri);
        }

        public void ProcessClaim(ProviderInformation provider, SubscriberInformation subscriber, List<ClaimLine> procedures)
        {
            Post<string>(new ProcessClaimRequest
            {
                // TODO PMS_ID = ,
                //PMS_ID = ...
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

        public Stream DownloadFile(DownoadFileRequest request)
        {
            return GetStream(null, request.url, HttpMethod.Get);
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
            var stream = GetStream(request, uri, HttpMethod.Post);
            if (stream != null)
            {
                var reader = new StreamReader(stream);
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            return null;
        }

        private Stream GetStream(object request, string uri, HttpMethod method)
        {
            var url = new Uri(new Uri(Url, UriKind.Absolute), uri);
            var webRequest = WebRequest.Create(url) as HttpWebRequest;
            webRequest.Headers.Clear();
            webRequest.Headers.Add("x-chewsi-token", _token);
            webRequest.Accept = "application/json";
            webRequest.ContentType = "application/json";
            webRequest.Method = method.ToString().ToUpper();
            
            if (_useProxy)
            {
                var proxy = new WebProxy(_proxyAddress, _proxyPort)
                {
                    Credentials = new NetworkCredential(_proxyUserName, _proxyPassword)
                };
                webRequest.Proxy = proxy;
            }

            // TODO
            //webRequest.Proxy = new WebProxy("http://localhost:8888");

            try
            {
                if (method == HttpMethod.Post)
                {
                    byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                    webRequest.ContentLength = data.Length;
                    using (Stream post = webRequest.GetRequestStream())
                    {
                        post.Write(data, 0, data.Length);
                    }
                }
                var response = webRequest.GetResponse() as HttpWebResponse;
                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return response.GetResponseStream();
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
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        // 500
                        Logger.Error("Error 500. Uri={0}", uri);
                        throw new InvalidOperationException("Error occured on the Chewsi server. Try again later");
                    }
                    Logger.Error("Unsupported status code {0}. Uri={1}", response.StatusCode, uri);
                    throw new InvalidOperationException($"Unsupported status code {response.StatusCode}");
                }
                Logger.Error("Empty response. Uri={0}", uri);
                throw new InvalidOperationException($"Empty response");
            }
            catch (WebException e)
            {
                var msg = "Unable to connect to Chewsi server. ";
                _dialogService.Show(msg + e.Message, "Error");
                Logger.Error(msg, e);
                return null;
            }
        }
    }
}
