﻿using EasyOAuth.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EasyOAuth.Providers.Web
{
    public class Google : OAuthImpl2
    {
        private string _redirectUri = null;
        private const string RequestTokenUrl = "https://accounts.google.com/o/oauth2/auth?client_id={0}&redirect_uri={1}&state={2}&scope={3}&response_type=code&approval_prompt=force";
        private const string AccessTokenUrl = "https://accounts.google.com/o/oauth2/token";
        private const string RenewTokenUrl = "https://accounts.google.com/o/oauth2/token";

        public Google(string RedirectUrl, string ConsumerKey, string ConsumerSecret, string Scope)
            : base(ConsumerKey, ConsumerSecret, Scope)
        {
            this._redirectUri = RedirectUrl;
            this.TokenExpires = DateTime.MaxValue;
        }

        public override void RequestToken()
        {
            this.OnReadyRedirect(new OAuthTokenReadyToRedirectEventArgs()
                {
                    RedirectUrl = string.Format(
                    RequestTokenUrl,
                    this.ClientKey, 
                    Utils.UrlEncode(this._redirectUri),
                    (new Random()).Next(1000000,8888888),
                    Utils.UrlEncode(this.Scope))
                });
        }

        public override void AccessToken(string LoginCallbackData)
        {
            var data = Utils.ParseQueryString(LoginCallbackData);

            // compatibility for ASP.NET HTTP Handler.
            if (data.AllKeys.Contains("error"))
            {
                throw new OAuthFailedException("ERROR_USER_DENIED",
                    string.Format("error: {0}, reason: {1}, description: {2}",
                    data["error"],
                    data["error_description"],
                    data["error_reason"]));
            }

            string code = null;

            if (data != null && data.AllKeys.Contains("code"))
                code = data["code"];
            else
                code = LoginCallbackData;

            var request = WebRequest.Create(AccessTokenUrl) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            var swriter = new StreamWriter(request.GetRequestStream());
            swriter.Write(string.Format("code={3}&client_id={0}&client_secret={2}&redirect_uri={1}&grant_type=authorization_code", 
                this.ClientKey,
                Utils.UrlEncode(this._redirectUri),
                this.ClientSecret,
                code));
            swriter.Close();

            try
            {
                var response = request.GetResponse();
                var sreader = new StreamReader(response.GetResponseStream());
                var json = JObject.Parse(sreader.ReadToEnd());

                this.Token = json.SelectToken("access_token").Value<string>();
                this.TokenExpires = DateTime.Now.AddSeconds(json.SelectToken("expires_in").Value<int>());
                sreader.Close();
            }
            catch (WebException we)
            {
                var errorResponse = we.Response as HttpWebResponse;
                string errorInfo = (new StreamReader(errorResponse.GetResponseStream())).ReadToEnd();

                if (errorResponse.StatusCode == HttpStatusCode.Unauthorized)
                    throw new OAuthFailedException("ERROR_UNAUTHORIZED", errorInfo);
                else if (errorResponse.StatusCode == HttpStatusCode.BadRequest)
                    throw new OAuthFailedException("ERROR_BAD_REQUEST", errorInfo);
                else
                    throw new OAuthFailedException("ERROR_OTHER", errorInfo);
            }
        }

        public override byte[] ConsumeService(Uri ServiceUri, string HttpMethod, byte[] Parameters)
        {
            var request = WebRequest.Create(ServiceUri) as HttpWebRequest;
            request.Method = HttpMethod;
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add("Authorization: Bearer " + Utils.UrlEncode(this.Token));

            if (HttpMethod == "POST" || HttpMethod == "PUT")
            {
                var requestStream = request.GetRequestStream();

                if (Parameters != null && Parameters.Length > 0)
                    requestStream.Write(Parameters, 0, Parameters.Length);

                requestStream.Close();
            }

            try
            {
                var response = request.GetResponse();
                var responseStream = response.GetResponseStream();
                var ms = new MemoryStream();
                byte[] data = new byte[4096];
                int readcount = 0;

                do
                {
                    readcount = responseStream.Read(data, 0, data.Length);

                    if (readcount == 0)
                        break;

                    ms.Write(data, 0, readcount);
                }
                while (readcount > 0);

                responseStream.Read(data, 0, data.Length);
                responseStream.Close();

                ms.Flush();
                ms.Position = 0;

                data = ms.ToArray();
                ms.Close();

                return data;
            }
            catch (WebException we)
            {
                var errorResponse = we.Response as HttpWebResponse;
                string errorInfo = (new StreamReader(errorResponse.GetResponseStream())).ReadToEnd();

                if (errorResponse.StatusCode == HttpStatusCode.Unauthorized)
                    throw new OAuthFailedException("ERROR_UNAUTHORIZED", errorInfo);
                else if (errorResponse.StatusCode == HttpStatusCode.BadRequest)
                    throw new OAuthFailedException("ERROR_BAD_REQUEST", errorInfo);
                else
                    throw new OAuthFailedException("ERROR_OTHER", errorInfo);
            }
        }

        public override void InvalidateToken()
        {
            var client = new WebClient();
            client.DownloadString(string.Format(
                "https://accounts.google.com/o/oauth2/revoke?token={0}", this.Token));
        }

        public override void RenewToken()
        {
            var request = WebRequest.Create(RenewTokenUrl) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            var swriter = new StreamWriter(request.GetRequestStream());
            swriter.Write(string.Format("client_id={0}&client_secret={1}&refresh_token={2}&grant_type=refresh_token",
                this.ClientKey,
                this.ClientSecret,
                this.Token));
            swriter.Close();

            try
            {
                var response = request.GetResponse();
                var sreader = new StreamReader(response.GetResponseStream());
                var json = JObject.Parse(sreader.ReadToEnd());

                this.Token = json.SelectToken("access_token").Value<string>();
                this.TokenExpires = DateTime.Now.AddSeconds(json.SelectToken("expires_in").Value<int>());
                sreader.Close();
            }
            catch (WebException we)
            {
                var errorResponse = we.Response as HttpWebResponse;
                string errorInfo = (new StreamReader(errorResponse.GetResponseStream())).ReadToEnd();

                if (errorResponse.StatusCode == HttpStatusCode.Unauthorized)
                    throw new OAuthFailedException("ERROR_UNAUTHORIZED", errorInfo);
                else if (errorResponse.StatusCode == HttpStatusCode.BadRequest)
                    throw new OAuthFailedException("ERROR_BAD_REQUEST", errorInfo);
                else
                    throw new OAuthFailedException("ERROR_OTHER", errorInfo);
            }
        }
    }
}
