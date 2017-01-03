﻿using Cloud.GoogleDrive.Oauth;
using CustomHttpRequest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

namespace Cloud.GoogleDrive
{
    public class DriveAPIHttprequestv2
    {
        public string Email = "";
        public string PassWord;
        bool acknowledgeAbuse = true;
        TokenGoogleDrive token = new TokenGoogleDrive();
        GoogleAPIOauth2 oauth;
        private static object SyncRefreshToken = new object();
        public HttpRequest_ request;
        public delegate void TokenRenewCallback(TokenGoogleDrive token, string Email);//, string PassWord);
        public event TokenRenewCallback TokenRenewEvent;
        public bool Debug = false;
        public TokenGoogleDrive GetToken
        {
            get { return token; }
            set { token = value; }
        }

        public DriveAPIHttprequestv2(TokenGoogleDrive token)
        {
            this.token = token;
            oauth = new GoogleAPIOauth2(token.refresh_token);
        }
        public DriveAPIHttprequestv2(string access_token, string refresh_token)
        {
            if ((string.IsNullOrEmpty(access_token) | string.IsNullOrEmpty(refresh_token)) == true) throw new Exception("Value input can't be null.");
            oauth = new GoogleAPIOauth2(refresh_token);
            this.token.access_token = access_token;
            this.token.refresh_token = refresh_token;
        }

        private object Request(string url, TypeRequest typerequest, TypeReturn typereturn = TypeReturn.string_, byte[] bytedata = null, string[] moreheader = null)
        {
            request = new HttpRequest_(url, typerequest.ToString());
            request.debug = Debug;
            if (typereturn == TypeReturn.streamresponse_) request.ReceiveTimeout = 5000;
            request.AddHeader("HOST: www.googleapis.com");
            request.AddHeader("Authorization", "Bearer " + token.access_token);
            if (moreheader != null)
            {
                foreach (string h in moreheader)
                {
                    request.AddHeader(h);
                }
            }
            if ((typerequest == TypeRequest.POST | typerequest == TypeRequest.DELETE) & bytedata == null) request.AddHeader("Content-Length: 0");
            if (bytedata != null & (typerequest == TypeRequest.POST | typerequest == TypeRequest.PATCH))
            {
                request.AddHeader("Content-Length: " + bytedata.Length.ToString());
                Stream stream = request.SendHeader_And_GetStream();
                stream.Write(bytedata, 0, bytedata.Length);
                stream.Flush();
                //Console.WriteLine(">> send data\r\n" + Encoding.UTF8.GetString(bytedata));
            }
            //get response
            try
            {
                if (typereturn == TypeReturn.header_response)
                {
                    string temp = request.GetTextDataResponse(false, true);
                    return request.HeaderReceive;
                }
                if (typereturn == TypeReturn.streamresponse_) return request.ReadHeaderResponse_and_GetStreamResponse(true, true);//get stream response
                else if (typereturn == TypeReturn.streamupload_) return request.SendHeader_And_GetStream();//get stream upload
                else { request.SendHeader_And_GetStream(); return request.GetTextDataResponse(true, true); }//text data response
            }
            catch (HttpException ex)
            {
                //string temp = request.HeaderReceive;
                string textdata = request.TextDataResponse;
                //Console.WriteLine(temp + "\r\n" + textdata);
                if (ex.GetHttpCode() == 401)
                {

                    if (Monitor.TryEnter(SyncRefreshToken))
                    {
                        try
                        {
                            Monitor.Enter(SyncRefreshToken);
                            token = oauth.RefreshToken();
                            TokenRenewEvent.Invoke(token, this.Email);//, this.PassWord);
                        }
                        finally { Monitor.Exit(SyncRefreshToken); }
                        return Request(url, typerequest, typereturn, bytedata, moreheader);
                    }
                    else
                    {
                        try { Monitor.Enter(SyncRefreshToken); }
                        finally { Monitor.Exit(SyncRefreshToken); }
                        return Request(url, typerequest, typereturn, bytedata, moreheader);
                    }
                }
                if (ex.GetHttpCode() == 403 & acknowledgeAbuse)
                {
                    return Request(url + "&acknowledgeAbuse=true", typerequest, typereturn, bytedata, moreheader);
                }
                if (typerequest == TypeRequest.DELETE && ex.GetHttpCode() == 204)// delete result
                {
                    return textdata;
                }
                if (ex.GetHttpCode() == 308 && typerequest == TypeRequest.PUT && typereturn == TypeReturn.streamupload_)
                {
                    return request.GetStream();
                }
                throw ex;
            }
        }

        public string About(bool includeSubscribed = true, long maxChangeIdCount = -1, long startChangeId = -1)
        {
            string url = "https://www.googleapis.com/drive/v2/about";
            string parameters = "";
            if (!includeSubscribed) parameters += "includeSubscribed=false";
            if (!(maxChangeIdCount == -1))
            {
                if (parameters.Length > 0)
                {
                    parameters += "&maxChangeIdCount=" + maxChangeIdCount;
                }
                else
                {
                    parameters = "maxChangeIdCount=" + maxChangeIdCount;
                }
            }
            if (!(startChangeId == -1))
            {
                if (parameters.Length > 0)
                {
                    parameters += "&startChangeId=" + startChangeId;
                }
                else
                {
                    parameters = "startChangeId=" + startChangeId;
                }
            }
            if (parameters.Length == 0) return (string)Request(url, TypeRequest.GET);
            return (string)Request(url, TypeRequest.GET, TypeReturn.string_, Encoding.UTF8.GetBytes(parameters));
        }


        #region Files
        public string Files_list(OrderByEnum[] order, string q = null, CorpusEnum corpus = CorpusEnum.DEFAULT,
                                ProjectionEnum projection = ProjectionEnum.BASIC, string pageToken = null,
                                int maxResults = 1000, SpacesEnum spaces = SpacesEnum.drive)
        {
            string url = string.Format(
                "https://www.googleapis.com/drive/v2/files?orderBy={0}&corpus={1}&projection={2}&maxResults={3}&spaces={4}",
                HttpUtility.UrlEncode(OrderBy.Get(order), Encoding.UTF8),
                corpus.ToString(),
                projection.ToString(),
                maxResults.ToString(),
                spaces.ToString()
                );
            if (pageToken != null) url += "&pageToken=" + pageToken;
            if (q != null) url += "&q=" + HttpUtility.UrlEncode(q, Encoding.UTF8);
            return (string)Request(url, TypeRequest.GET);
        }

        public Stream Files_get(string fileId, long PosStart = 0, long endpos = 0)//,string alt = "media",bool acknowledgeAbuse = true,
                                                                                  //ProjectionEnum projection = ProjectionEnum.BASIC,bool updateViewedDate = false)
        {
            string url = string.Format("https://www.googleapis.com/drive/v2/files/{0}?alt=media", fileId);
            string[] moreheader = { "Range: bytes=" + PosStart.ToString() + "-" + endpos.ToString() };
            return (Stream)Request(url, TypeRequest.GET, TypeReturn.streamresponse_, null, (endpos != 0) ? moreheader : null);
        }

        public string Files_insert_resumable_getUploadID(string jsondata, string typefileupload, long filesize)
        {
            string url = string.Format("https://www.googleapis.com/upload/drive/v2/files?uploadType={0}", uploadType.resumable.ToString());
            string[] moreheader = {
                                "Content-Type: application/json; charset=UTF-8",
                                "X-Upload-Content-Type: " + typefileupload,
                                "X-Upload-Content-Length: " + filesize.ToString() };
            string data = (string)Request(url, TypeRequest.POST, TypeReturn.header_response, Encoding.UTF8.GetBytes(jsondata), moreheader);
            string[] arrheader = Regex.Split(data, "\r\n");
            foreach (string h in arrheader)
            {
                if (h.ToLower().IndexOf("location: ") >= 0)
                {
                    string location = Regex.Split(h, ": ")[1];
                    if (string.IsNullOrEmpty(location)) throw new NullReferenceException(data);
                    return location;
                }
            }
            throw new Exception("Can't get data: \r\n" + data);
        }

        public Stream Files_insert_resumable(string url_uploadid, long posstart, long posend, long filesize)
        {
            List<string> moreheader = new List<string>(){ "Content-Length: " + (posend - posstart + 1).ToString(),
                "Content-Type: image/jpeg",
                "Connection: keep-alive",
                "Content-Range: bytes " + posstart.ToString() + "-" + posend.ToString()+"/" + filesize.ToString()
            };
            return (Stream)Request(url_uploadid, TypeRequest.PUT, TypeReturn.streamupload_, null, moreheader.ToArray());
        }

        public string GetResponse_Files_insert_resumable()
        {
            Console.WriteLine(request.GetTextDataResponse(false, false));
            return request.HeaderReceive;
        }

        public string Files_delete(string fileId)
        {
            return (string)Request("https://www.googleapis.com/drive/v2/files/" + fileId + "?key=" + APPkey.APIKey, TypeRequest.DELETE, TypeReturn.string_, null, null);
        }

        public string CreateFolder(string name, string parent_id)
        {
            string data = "{\"mimeType\": \"application/vnd.google-apps.folder\", \"title\": \"" + name + "\", \"parents\": [{\"id\": \"" + parent_id + "\"}]}";
            return (string)Request("https://www.googleapis.com/drive/v2/files", TypeRequest.POST, TypeReturn.string_, Encoding.UTF8.GetBytes(data),
                new string[] { "Content-Type: application/json" });
        }

        public string ItemTrash(string id)
        {
            return (string)Request("https://www.googleapis.com/drive/v2/files/" + id + "/trash?fields=labels%2Ftrashed&key=" + APPkey.APIKey, TypeRequest.POST, TypeReturn.string_, null, null);
        }

        public string EditMetaData(string iditem, string json_data = null)//json_data = null is get metadata
        {
            byte[] buffer = null;
            if (!string.IsNullOrEmpty(json_data)) buffer = Encoding.UTF8.GetBytes(json_data);
            return (string)Request("https://www.googleapis.com/drive/v2/files/" + iditem + "?key=" + APPkey.APIKey + "&alt=json",
                    TypeRequest.PATCH, TypeReturn.string_, buffer,
                    new string[] { "Content-Type: application/json" });
        }

        #endregion


    }

}