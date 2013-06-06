﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ArcGIS.ServiceModel.Common;
using ArcGIS.ServiceModel.Operation;

namespace ArcGIS.ServiceModel.Logic
{    
    public interface IPortalGateway
    {
        /// <summary>
        /// Made up of scheme://host:port/site
        /// </summary>
        String RootUrl { get; }
        Token Token { get; }
        ISerializer Serializer { get; set; }
    }

    /// <summary>
    /// Used for (de)serializtion of requests and responses. 
    /// </summary>
    /// <remarks>Split out as interface to allow injection. Also moves implementation out of this
    /// library so it can use whatever framework the developer wants.</remarks>
    public interface ISerializer
    {
        /// <summary>
        /// Convert an object into a dictionary
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectToConvert"></param>
        /// <returns></returns>
        Dictionary<String, String> AsDictionary<T>(T objectToConvert) where T : CommonParameters;

        /// <summary>
        /// Deserialize string as a <see cref="PortalResponse"/>"/>
        /// </summary>
        /// <typeparam name="T">The type of the result from the call</typeparam>
        /// <param name="dataToConvert">Json string to deserialize</param>
        /// <returns></returns>
        T AsPortalResponse<T>(String dataToConvert) where T : PortalResponse;
    }

    public abstract class PortalGateway : IPortalGateway
    {
        const String AGOPortalUrl = "http://www.arcgis.com/sharing/rest/";
        readonly String _username;
        readonly String _password;
        readonly String _urlPrefix;

        protected PortalGateway()
            : this(AGOPortalUrl, String.Empty, String.Empty)
        { }

        protected PortalGateway(String rootUrl)
            : this(rootUrl, String.Empty, String.Empty)
        { }

        protected PortalGateway(String username, String password)
            : this(AGOPortalUrl, username, password)
        { }

        protected PortalGateway(String rootUrl, String username, String password)
        {
            rootUrl = rootUrl.TrimEnd('/');
            rootUrl = rootUrl.Replace("/rest/services", "");

            RootUrl = rootUrl.ToLower() + '/';
            _urlPrefix = RootUrl + "rest/services/";
            
            _username = username;
            _password = password;
        }

        public string RootUrl { get; private set; }

        public Token Token { get; private set; }

        public ISerializer Serializer { get; set; }

        async Task<Token> CheckGenerateToken()
        {
            if (String.IsNullOrWhiteSpace(_username) && String.IsNullOrWhiteSpace(_password)) return null;
            if (Token != null && !Token.IsExpired) return Token;

            Token = null;
            var tokenRequest = new GenerateToken { Username = _username, Password = _password };

            return await Post<Token>(RootUrl.Replace("http://", "https://") + tokenRequest.RelativeUrl.Trim('/'), Serializer.AsDictionary(tokenRequest));
        }

        String AsRequestQueryString<T>(T objectToConvert) where T : CommonParameters
        {
            var dictionary = Serializer.AsDictionary(objectToConvert);

            // TODO : add url encoding to key and value
            return "?" + String.Join("&", dictionary.Keys.Select(k => String.Format("{0}={1}", k, dictionary[k])));
        }
        
        public async Task<PortalResponse> Ping(IEndpoint endpoint)
        {
            return await Get<PortalResponse>(endpoint.RelativeUrl);
        }

        protected async Task<T1> Get<T1, T2>(T2 requestObject)
            where T2 : CommonParameters
            where T1 : PortalResponse
        {
            return await Get<T1>(AsRequestQueryString(requestObject));
        }

        protected async Task<T> Get<T>(String endpoint) where T : PortalResponse
        {
            if (Serializer == null) throw new NullReferenceException("Serializer has not been set.");

            var token = await CheckGenerateToken();

            if (token != null && !String.IsNullOrWhiteSpace(token.Value) && !endpoint.Contains("token="))
                endpoint += (endpoint.Contains("?") ? "&" : "?") + "token=" + token.Value;
            if (!endpoint.Contains("f="))
                endpoint += (endpoint.Contains("?") ? "&" : "?") + "f=json";

            // check the url is complete (ignore the scheme)
            if (!endpoint.Contains(_urlPrefix.Substring(6)) && !endpoint.Contains(RootUrl.Substring(6))) endpoint = _urlPrefix + endpoint; 

            // TODO : use POST if request is too long
            
            using (var handler = new HttpClientHandler())
            {
                // handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var httpClient = new HttpClient(handler))
                {
                    HttpResponseMessage response = await httpClient.GetAsync(endpoint);
                    response.EnsureSuccessStatusCode();

                    var result = Serializer.AsPortalResponse<T>(await response.Content.ReadAsStringAsync());

                    if (result.Error != null)
                        throw new InvalidOperationException(result.Error.ToString());

                    return result;
                }
            }
        }

        protected async Task<T1> Post<T1, T2>(String endpoint, T2 requestObject)
            where T2 : CommonParameters
            where T1 : PortalResponse
        {
            return await Post<T1>(endpoint, Serializer.AsDictionary(requestObject));
        }

        protected async Task<T> Post<T>(String endpoint, Dictionary<String, String> parameters) where T : PortalResponse
        {
            if (Serializer == null) throw new NullReferenceException("Serializer has not been set.");

            // these should have already been added
            if (!parameters.ContainsKey("f"))
                parameters.Add("f", "json");
            if (!parameters.ContainsKey("token") && Token != null && !String.IsNullOrWhiteSpace(Token.Value))
                parameters.Add("token", Token.Value);

            // check the url is complete (ignore the scheme)
            if (!endpoint.Contains(_urlPrefix.Substring(6)) && !endpoint.Contains(RootUrl.Substring(6))) endpoint = _urlPrefix + endpoint;

            HttpContent content = new FormUrlEncodedContent(parameters);
            using (var handler = new HttpClientHandler())
            {
                //handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var httpClient = new HttpClient(handler))
                {
                    HttpResponseMessage response = await httpClient.PostAsync(endpoint, content);
                    response.EnsureSuccessStatusCode();
                    var result = Serializer.AsPortalResponse<T>(await response.Content.ReadAsStringAsync());
                    if (result.Error != null)
                        throw new InvalidOperationException(result.Error.ToString());

                    return result;
                }
            }
        }
    }
}