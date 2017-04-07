﻿using Core.CloudSubClass;
using Core.StaticClass;
using SupDataDll;
using SupDataDll.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Core.Class
{
    public class HostStreamingFileViaHttp
    {
        HttpListener listener;
        public void Start()
        {
            if(AppSetting.settings.GetSettingsAsBool(SupDataDll.SettingsKey.HOST_STREAM))
            {
                int port = 34567;
                int.TryParse(AppSetting.settings.GetSettingsAsString(SupDataDll.SettingsKey.HOST_STREAM_PORT), out port);
                listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:" + port);
                listener.Start();
                listener.BeginGetContext(new AsyncCallback(RecieveCode), null);
            }
        }


        void RecieveCode(IAsyncResult rs)
        {
            HttpListenerContext ls = listener.EndGetContext(rs);

            HttpListenerRequest request = ls.Request;
            string cloudname = request.QueryString.Get("cloudname");
            string id = request.QueryString.Get("id");
            string path = request.QueryString.Get("path");
            string email = request.QueryString.Get("email");
            string range = request.Headers.Get("Range");

            CloudType type = CloudType.Folder;
            Stream stream;
            long start_range = -1;
            long end_range = -1;
            ExplorerNode filenode = null;

            if (range != null)
            {
                string[] range_arr = range.Split('-');
                long.TryParse(range_arr[0], out start_range);
                long.TryParse(range_arr[1], out end_range);
            }

            if (cloudname != null && id != null && Enum.TryParse<CloudType>(cloudname, out type) && type != CloudType.LocalDisk && type != CloudType.Folder)
            {



                if (email == null) email = AppSetting.settings.GetDefaultCloud(type);
                ExplorerNode rootnode = AppSetting.settings.GetCloudRootNode(email, type);
                filenode = new ExplorerNode(new NodeInfo() {  ID = id });
                rootnode.AddChild(filenode);                
            }
            else if(path != null && File.Exists(path))
            {
                type = CloudType.LocalDisk;
                filenode = ExplorerNode.GetNodeFromDiskPath(path);
            }
            else//return 404 not found
            {

            }

            stream = AppSetting.ManageCloud.GetFileStream(filenode, start_range, end_range, false);//mega need cal chunk and size file

            HttpListenerResponse response = ls.Response;



        }



    }
}
