﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using static AbloadPush.ImageService.UploadResult;

namespace AbloadPush.ImageService.Abload
{
    class AbloadService : IImageServiceProvider
    {        
        public static readonly string Address = "https://abload.de";
        public static readonly string ImageAddress = Address + "/img/";
        public static readonly string UploadAddress = Address + "/upload.php?printr=true";
        public static readonly string LoginAddress = Address + "/login.php";
        public static readonly string LogoutAddress = Address + "/calls/logout.php";
        public static readonly string UserSettingsAddress = Address + "/calls/userXML.php";

        public static readonly string PostBoundaryName = "--atool26589";
        public static readonly string PostResizeFieldName = "\"resize\"";
        public static readonly string PostGalleryFieldName = "\"gallery\"";
        public static readonly string PostImageFieldName = "\"img0\"";

        public static readonly string SessionCookieName = "ablgntan";

        private static readonly string AbloadResponsePattern = @"\s*\[newname\]\s*=>\s*(?<filename>.+)\s*";      

        private readonly Uri uri = new Uri(Address);

        private readonly CookieContainer cookieContainer;
        private readonly HttpClientHandler clientHandler;
        private readonly HttpClient client;

        private UploadResult lastUploadResult;

        public string Name => Address;
        public UploadResult LastUploadResult => lastUploadResult;

        public event EventHandler<LoginResult> LoginComplete;
        public event EventHandler<LogoutResult> LogoutComplete;
        public event EventHandler<UploadResult> UploadFinished;

        public AbloadService(CookieContainer cookieContainer)
        {      
            this.cookieContainer = cookieContainer;

            clientHandler = new HttpClientHandler();
            clientHandler.CookieContainer = cookieContainer;
            //clientHandler.AllowAutoRedirect = false;

            client = new HttpClient(clientHandler);

            // TODO: get galleries
        }

        public async void Login(ILoginData loginData)
        {
            var result = new LoginResult();

            var postContent = new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "name", loginData.Identifier },
                { "password", loginData.Secret },
                { "cookie", "on" }
            });

            var response = await client.PostAsync(LoginAddress, postContent);

            var abloadCookies = cookieContainer.GetCookies(new Uri(Address));
            var sessionCookie = abloadCookies[SessionCookieName];

            result.Success = true;

            LoginComplete(this, result);
        }

        public async Task<bool> IsLoggedIn()
        {
            var response = await client.GetAsync(UserSettingsAddress);

            if (new Uri(Address + response.RequestMessage.RequestUri.AbsolutePath) != new Uri(LoginAddress))
            {
                var responseString = await response.Content.ReadAsStringAsync();

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(responseString);
                var users = doc.GetElementsByTagName("user");
                return users.Count >= 1;
            }

            return false;
        }

        public async void Logout()
        {
            var result = new LogoutResult();

            var response = await client.GetAsync(LogoutAddress);

            LogoutComplete(this, result);
        }

        public async void Upload(Stream imageData)
        {
            var result = new UploadResult();

            var postContent = new MultipartFormDataContent(PostBoundaryName)
            {
                { new StringContent("none"), PostResizeFieldName },
                { new StringContent("NULL"), PostGalleryFieldName },
                { new StreamContent(imageData), PostImageFieldName, "\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-push.png\"" }
            };

            var response = await client.PostAsync(UploadAddress, postContent);
            var responseString = await response.Content.ReadAsStringAsync();

            var match = Regex.Match(responseString, AbloadResponsePattern);

            if (match.Success)
            {
                result.Status = UploadStatus.Succeeded;
                result.ImageUrl = ImageAddress + match.Groups["filename"].Value;
            }
            else
            {
                result.Status = UploadStatus.Failed;
                result.Reason = ("File not found in response"); 
            }

            lastUploadResult = result;
            UploadFinished(this, result);
        }



        private void LoadAbloadSettings()
        {
            // TODO!
        }
    }
}
