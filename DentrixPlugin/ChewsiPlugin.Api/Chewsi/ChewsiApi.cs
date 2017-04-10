using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using ChewsiPlugin.Api.Interfaces;
using Newtonsoft.Json;
using NLog;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ChewsiApi : IChewsiApi
    {
        private const string PublicKey = "<RSAKeyValue><Modulus>wucbBr9ssgvKqQwuJ+NNhnYs2ZZSePy6gCOcMFIxOPKDrqS3cLnaAhlTUOpz/zXGsFq9riLvOAy6j7U3rvHfdg+bNc8TkKX0QysuDWe17+YENU0rdoTTMOBEFjCfXpWR4SxfxJPAaTZvBi/ZDrk5KzF0JUr4PyfTP+tMHwpJU97AN7cM8eEMWoyP1yigiKgZH3JI2jfE/zigLgJPJ09htAriVIx3eMDPXxtfnvO1o8PSZbqcJvNB8I31dWepsPkGXgRBjJNY7IM7FYfEVN5KUrqmequafOZi2bzXJFchry23+f0GrvfY4Noj2Zq3M9/sjNiAGvKopkoWcCeXh41wsQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        private readonly IDialogService _dialogService;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _token;
        private bool _useProxy;
        private string _proxyAddress;
        private int _proxyPort;
        private string _proxyUserName;
        private string _proxyPassword;
        private const string Url = "http://chewsi-stage-txapi.azurewebsites.net";
            //"http://chewsi-dev-txapi-predeploy.azurewebsites.net";
            //"http://chewsi-dev-txapi.azurewebsites.net";
            //"http://chewsi-dev.azurewebsites.net/TXApi/";
            // https://www.chewsidental.com/TXAPI/
        private const string ValidateSubscriberAndProviderUri = "ValidateSubscriberAndProvider";
        private const string ProcessClaimUri = "ProcessClaim";
        private const string RegisterPluginUri = "RegisterPlugin";
        private const string Request835DownloadsUri = "Request835Downloads";
        private const string RequestClaimProcessingStatusUri = "RequestClaimProcessingStatus";
        private const string DownloadFileUri = "DownloadFile";
        private const string UpdatePluginRegistrationUri = "UpdatePluginRegistration";

        public ChewsiApi(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        /// <summary>
        /// Validates the subscriber and provider.
        /// </summary>
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
                SubscriberDOB = subscriber.SubscriberDateOfBirth?.ToString("d"),
                SubscriberFirstName = subscriber.SubscriberFirstName,
                SubscriberLastName = subscriber.SubscriberLastName,
                ChewsiID = subscriber.Id ?? ""
            },
                ValidateSubscriberAndProviderUri, true);
        }

        public void ProcessClaim(ProviderInformation provider, SubscriberInformation subscriber, List<ClaimLine> procedures)
        {
            Post<string>(new ProcessClaimRequest
            {
                PMS_ID = Guid.NewGuid().ToString(),
                TIN = provider.TIN,
                OfficeNbr = provider.OfficeNbr,
                ClaimLines = procedures,
                NPI = provider.NPI,
                // TODO PIN = ,
                ProviderID = provider.Id,
                SubscriberDOB = subscriber.SubscriberDateOfBirth?.ToString("d"),
                SubscriberFirstName = subscriber.SubscriberFirstName,
                PatientFirstName = subscriber.PatientFirstName,
                // TODO Should it be Chewsi Id?
                SubscriberID = subscriber.Id,
                SubscriberLastName = subscriber.SubscriberLastName,
                PatientLastName = subscriber.PatientLastName
            },
            ProcessClaimUri, true);
        }

        /// <summary>
        /// Registers the plugin. Returns machine Id.
        /// </summary>
        public string RegisterPlugin(RegisterPluginRequest request)
        {
            var response = Post<string>(request, RegisterPluginUri, true);
            return response;
        }

        public void UpdatePluginRegistration(UpdatePluginRegistrationRequest request)
        {
            Post<string>(request, UpdatePluginRegistrationUri, true);
        }

        public Request835DownloadsResponse Get835Downloads(Request835Downloads request)
        {
            return Post<Request835DownloadsResponse>(request, Request835DownloadsUri, true);
        }
        
        public ClaimProcessingStatusResponse GetClaimProcessingStatus(ClaimProcessingStatusRequest request)
        {
            return Post<ClaimProcessingStatusResponse>(request, RequestClaimProcessingStatusUri, false);
        }

        public Stream DownloadFile(DownoadFileRequest request)
        {
            return GetStream(request, DownloadFileUri, HttpMethod.Post, true);
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
        
        private T Post<T>(object request, string uri, bool showErrors) where T: class
        {
            var stream = GetStream(request, uri, HttpMethod.Post, showErrors);
            if (stream != null)
            {
                var reader = new StreamReader(stream);
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            return null;
        }

        private string GetSignature()
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(PublicKey);
                byte[] encryptedBytes = rsa.Encrypt(Encoding.UTF8.GetBytes(_token + "_" + DateTime.UtcNow), false);
                return Convert.ToBase64String(encryptedBytes);
            }
        }

        private Stream GetStream(object request, string uri, HttpMethod method, bool showErrors)
        {
            var url = new Uri(new Uri(Url, UriKind.Absolute), uri);
            var webRequest = WebRequest.Create(url) as HttpWebRequest;
            webRequest.Headers.Clear();
            webRequest.Headers.Add("x-chewsi-authid", GetSignature());
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
                }
                Logger.Error("Empty response. Uri={0}", uri);
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var response = (HttpWebResponse) e.Response;
                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        // 204
                        return null;
                    }
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // 401
                        Logger.Error("Error 401. Uri={0}", uri);
                        // throw new InvalidOperationException("Wrong user name or password");
                        // Server may respond with code 401 in case if data is inconsistent; for example when TIN not found on the server
                        return null;
                    }
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        // 404
                        Logger.Error("Error 404. Uri={0}", uri);
                    }
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        // 500
                        Logger.Error("Error 500. Uri={0}", uri);
                    }
                    Logger.Error("Unsupported status code {0}. Uri={1}", response.StatusCode, uri);                
                }
                var msg = "Unable to connect to Chewsi server. ";
                if (showErrors)
                {
                    _dialogService.Show(msg + e.Message, "Error");
                }
                Logger.Error(msg, e);
            }
            return null;
        }
    }
}
