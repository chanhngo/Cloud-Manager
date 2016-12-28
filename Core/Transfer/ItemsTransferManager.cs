﻿using Core.cloud;
using Core.StaticClass;
using DropboxHttpRequest;
using GoogleDriveHttprequest;
using Newtonsoft.Json;
using SupDataDll;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;

namespace Core.Transfer
{
    public class ItemsTransferManager
    {
        public bool AreCut = false;//delete from after transfer
        public List<Thread> ThreadsItemLoadWork = new List<Thread>();
        public TransferGroup group = new TransferGroup();
        List<TransferBytes> ItemsTransfer = new List<TransferBytes>();

        //loading
        List<NewTransferItem> items;
        public AnalyzePath fromfolder;
        public AnalyzePath savefolder;
        
        #region Declare
        //load from save data
        internal ItemsTransferManager(JsonDataSaveGroup group_json)
        {
            this.group = group_json.Group;
            this.fromfolder = new AnalyzePath(group_json.fromfolder_raw);
            this.AreCut = group_json.AreCut;
            this.savefolder = new AnalyzePath(group_json.savefolder_raw);

            this.group.status = (group_json.Group.status == StatusUpDown.Done | group_json.Group.status == StatusUpDown.Error) ? group_json.Group.status : StatusUpDown.Stop;
            foreach (TransferItem item in this.group.items)
            {
                if (item.status == StatusUpDown.Running) item.status = StatusUpDown.Stop;
                item.col[3] = item.status.ToString();
            }
            this.group.change = ChangeTLV.Done;
            RefreshGroupDataToShow(-1);
        }
        //load from user
        public ItemsTransferManager(List<NewTransferItem> items, string fromfolder_raw, string savefolder_raw, bool AreCut = false)
        {
            if (items.Count == 0) throw new Exception("List<NewTransferItem> items count = 0");
            if (string.IsNullOrEmpty(fromfolder_raw)) throw new ArgumentNullException(fromfolder_raw);
            if (string.IsNullOrEmpty(savefolder_raw)) throw new ArgumentNullException(savefolder_raw);
            this.items = items;
            this.fromfolder = new AnalyzePath(fromfolder_raw);
            this.savefolder = new AnalyzePath(savefolder_raw);
            this.AreCut = AreCut;
        }
        #endregion

        #region Load List File
        public void LoadListItems()
        {
            this.group.col = new List<string> { fromfolder.Path_Raw, savefolder.Path_Raw, this.group.status.ToString(), "0/0", "", "", "" };
            AppSetting.uc_lv_ud_instance.AddNewGroup(group);
            foreach (NewTransferItem item in items)
            {
                if (item.type == Type_FileFolder.File)
                    group.items.Add(LoadFile(fromfolder.PathIsUrl ? fromfolder.ID : fromfolder.Path_Raw, item.name, item.size, item.id));
                else
                {
                    group.items.AddRange(
                        ListAllItemInFolder(
                            fromfolder.PathIsUrl ? fromfolder.ID + "/" + item.name : fromfolder.Path_Raw + (fromfolder.PathIsCloud ? "/" : "\\") + item.name,
                            item.id
                            ));
                }
            }
            group.status = StatusUpDown.Waiting;
        }
        List<TransferItem> ListAllItemInFolder(string path_rawItem, string id = "")
        {
            ListItemFileFolder list;
            List<TransferItem> ud_items = new List<TransferItem>();
            try
            {
                if (fromfolder.PathIsUrl) list = AppSetting.ManageCloud.GetItemsList("", id);
                else list = AppSetting.ManageCloud.GetItemsList(path_rawItem, id);//UnauthorizedAccessException
            }
            catch (UnauthorizedAccessException ex) { return ud_items; }

            foreach (FileFolder ffitem in list.Items)
            {
                if (ffitem.Size == -1)//folder size = -1
                    ud_items.AddRange(
                        ListAllItemInFolder(
                            fromfolder.PathIsUrl ? path_rawItem + "/" + ffitem.Name : path_rawItem + (fromfolder.PathIsCloud ? "/" : "\\") + ffitem.Name,
                            ffitem.id));
                else
                    ud_items.Add(
                        LoadFile(
                            path_rawItem,
                            ffitem.Name,
                            ffitem.Size,
                            ffitem.id));
            }
            return ud_items;
        }
        //Path_Parent : id/folder/folder or GD:a@gmail.com/folder/folder
        TransferItem LoadFile(string Path_Parent, string FileName, long size, string FileId)//Path_raw path parent folder of file
        {
            TransferItem ud_item = new TransferItem();
            //From
            ud_item.From.path = Path_Parent + (fromfolder.PathIsCloud ? "/" : "\\") + FileName;// id/filename or GD:a@gmail.com/filename
            ud_item.From.Fileid = FileId;
            ud_item.From.email = fromfolder.PathIsUrl ? AppSetting.settings.GetDefaultCloud(fromfolder.TypeCloud) : fromfolder.Email;// if url -> get defaul email
            ud_item.From.TypeCloud = fromfolder.TypeCloud;
            ud_item.From.Size = size;
            ud_item.From.filename = FileName;
            //group & UI
            group.TotalFileLength += size;
            ud_item.SizeString = UnitConventer.ConvertSize(size, 2, UnitConventer.unit_size);
            ud_item.status = StatusUpDown.Waiting;
            //To
            ud_item.To.filename = FileName;
            ud_item.To.path = AnalyzePath.GetPathTo(fromfolder.PathIsUrl ? ud_item.From.path : (new AnalyzePath(ud_item.From.path)).GetPath(), fromfolder, savefolder);//
            ud_item.To.email = savefolder.Email;
            ud_item.To.TypeCloud = savefolder.TypeCloud;
            ud_item.col = new List<string> { ud_item.From.path, ud_item.To.path, ud_item.status.ToString(), "", "", "", "" };
            return ud_item;
        }
        #endregion

        public void ManagerItemsAndRefreshData()
        {
            //clean thread
            for (int i = 0; i < ThreadsItemLoadWork.Count; i++)
            {
                if (!ThreadsItemLoadWork[i].IsAlive)
                {
                    ThreadsItemLoadWork.RemoveAt(i);
                    i--;
                }
            }

            switch (group.status)
            {
                case StatusUpDown.Loading: return;
                case StatusUpDown.Started: group.status = StatusUpDown.Running; group.Timestamp = CurrentMillis.Millis; break;
                case StatusUpDown.Remove: group.status = StatusUpDown.Removing; return;
                case StatusUpDown.Removing: return;
            }

            #region Count
            int count_item_running = 0;
            int count_item_done = 0;
            int count_item_error = 0;
            int count_item_stop = 0;
            for (int i = 0; i < group.items.Count; i++)
            {
                if (group.items[i].status == StatusUpDown.Remove)
                {
                    group.items.RemoveAt(i);
                    i--;
                    break;
                }
                else switch (group.items[i].status)
                    {
                        case StatusUpDown.Running: count_item_running++; break;
                        case StatusUpDown.Done: count_item_done++; break;
                        case StatusUpDown.Error: count_item_error++; break;
                        case StatusUpDown.Stop: count_item_stop++; break;
                        default: break;
                    }

                //clear speed download when not running
                if (group.items[i].status != StatusUpDown.Running)
                {
                    if (!string.IsNullOrEmpty(group.items[i].col[4])) group.items[i].col[4] = "";
                    if (!string.IsNullOrEmpty(group.items[i].col[5])) group.items[i].col[5] = "";
                }
            }
            #endregion

            #region Running group
            if (this.group.status == StatusUpDown.Running && count_item_done + count_item_error + count_item_stop != this.group.items.Count)
            {
                long Group_TotalTransfer = 0;
                for (int i = 0; i < group.items.Count; i++)//start item waiting and Started(force)
                {
                    Group_TotalTransfer += group.items[i].Transfer;
                    if (this.group.items[i].status == StatusUpDown.Started)// start item force start
                    {
                        Thread thr = new Thread(WorkThread);
                        this.group.items[i].status = StatusUpDown.Running;
                        thr.Start(i);
                        ThreadsItemLoadWork.Add(thr);
                        count_item_running++;
                    }
                    if (group.items[i].status == StatusUpDown.Waiting && count_item_running < group.MaxItemsDownload)//start item waiting
                    {
                        Thread thr = new Thread(WorkThread);
                        group.items[i].status = StatusUpDown.Running;
                        group.items[i].Timestamp = Stopwatch.GetTimestamp();
                        thr.Start(i);
                        ThreadsItemLoadWork.Add(thr);
                        count_item_running++;
                    }
                    #region caculate speed & time left item
                    if (this.group.items[i].status == StatusUpDown.Running)
                    {
                        long size_transfer = group.items[i].Transfer - group.items[i].OldTransfer;
                        long time_milisec = CurrentMillis.Millis - group.items[i].Timestamp;
                        if (time_milisec != 0 & time_milisec >= 500)
                        {
                            //speed
                            group.items[i].Timestamp = CurrentMillis.Millis;
                            group.items[i].OldTransfer = group.items[i].Transfer;
                            decimal speed = (decimal)size_transfer * 1000 / time_milisec;
                            group.items[i].col[4] = UnitConventer.ConvertSize(speed, 2, UnitConventer.unit_speed);
                            //time 
                            if (speed != 0)
                            {
                                long length_left = group.items[i].From.Size - group.items[i].Transfer;
                                long secondleft = length_left / decimal.ToInt64(speed);
                                group.items[i].col[5] = CurrentMillis.GetTimeBySecond((int)secondleft);
                            }
                        }
                    }
                    #endregion
                }
                #region caculate speed & time left group
                long time_milisec_group = CurrentMillis.Millis - group.Timestamp;
                if (time_milisec_group != 0 & time_milisec_group >= 500)
                {
                    //speed
                    group.Timestamp = CurrentMillis.Millis;
                    decimal speed_group = (decimal)(Group_TotalTransfer - group.OldTransfer) * 1000 / time_milisec_group;
                    group.OldTransfer = Group_TotalTransfer;
                    group.col[4] = UnitConventer.ConvertSize(speed_group, 2, UnitConventer.unit_speed);
                    //time left
                    if (speed_group != 0)
                    {
                        long length_left_group = group.TotalFileLength - Group_TotalTransfer;
                        long secondleft_group = length_left_group / decimal.ToInt64(speed_group);
                        group.col[5] = CurrentMillis.GetTimeBySecond((int)secondleft_group);
                    }
                }
                #endregion
            }
            else
            {
                if (this.group.status == StatusUpDown.Running)
                {
                    if (this.AreCut && count_item_done == this.group.items.Count)// remove cut
                    {
                        DeleteItems list = new DeleteItems();
                        list.PernamentDelete = false;
                        foreach (NewTransferItem item in items)
                        {
                            list.items.Add(fromfolder.Path_Raw + (fromfolder.PathIsCloud ? "/" : "\\") + item.name);
                        }
                        Thread thr = new Thread(AppSetting.ManageCloud.Delete);
                        thr.Start(list);
                    }
                    this.group.status = StatusUpDown.Done;//Done group
                }
            }
            #endregion

            #region Change LV
            if (this.group.change == ChangeTLV.Processing && (this.group.status == StatusUpDown.Done |
                this.group.status == StatusUpDown.Error | this.group.status == StatusUpDown.Stop))
                this.group.change = ChangeTLV.ProcessingToDone;
            if (this.group.change == ChangeTLV.Done && (this.group.status == StatusUpDown.Started | this.group.status == StatusUpDown.Running |
                this.group.status == StatusUpDown.Waiting | this.group.status == StatusUpDown.Loading))
                this.group.change = ChangeTLV.DoneToProcessing;
            #endregion

            RefreshGroupDataToShow(count_item_done);
        }

        void RefreshGroupDataToShow(int count_item_done)
        {
            if (count_item_done != -1 & group.col[3].IndexOf("100% (") < 0 & group.items.Count != 0)
                group.col[3] = Math.Round((double)count_item_done * 100 / group.items.Count, 2).ToString() + "% (" + count_item_done.ToString() + "/" + group.items.Count.ToString() + ")";
            group.col[2] = group.status.ToString();
            for (int i = 0; i < group.items.Count; i++)
            {
                group.items[i].col[2] = group.items[i].status.ToString();
                if (group.items[i].col[3].IndexOf("100% (") < 0 & group.items[i].From.Size != 0)
                    group.items[i].col[3] = Math.Round((double)group.items[i].Transfer * 100 / group.items[i].From.Size, 2).ToString() + "% (" + UnitConventer.ConvertSize(group.items[i].Transfer, 2, UnitConventer.unit_size) + "/" + group.items[i].SizeString + ")";
                group.items[i].col[6] = group.items[i].ErrorMsg;
            }
        }

        void WorkThread(object obj)
        {
            int x = (int)obj;
            try
            {
                group.items[x].To.ap = new AnalyzePath(group.items[x].To.path);

                #region CreateStreamFrom
                if (!fromfolder.PathIsUrl)
                {
                    //group.items[x].From.ap = new AnalyzePath(group.items[x].From.path);
                    group.items[x].From.stream = AppSetting.ManageCloud.GetFileStream(group.items[x].From.path,
                        group.items[x].From.Fileid,
                        group.items[x].To.ap.PathIsCloud,
                        group.items[x].TransferRequest,
                        group.items[x].From.Size - 1);
                }
                else
                {
                    group.items[x].From.stream = AppSetting.ManageCloud.GetFileStream("",
                        group.items[x].From.Fileid,
                        group.items[x].To.ap.PathIsCloud,
                        group.items[x].TransferRequest,
                        group.items[x].From.Size - 1,
                        fromfolder.TypeCloud,
                        AppSetting.settings.GetDefaultCloud(fromfolder.TypeCloud));
                }
                #endregion

                int buffer_length = 32;//default
                int.TryParse(AppSetting.settings.GetSettingsAsString(SettingsKey.BufferSize), out buffer_length);//get buffer_length from setting
                group.items[x].buffer = new byte[buffer_length * 1024];//create buffer

                group.items[x].byteread = 0;
                string token = "";
                if (group.items[x].To.ap.PathIsCloud) token = AppSetting.settings.GetToken(group.items[x].To.ap.Email, group.items[x].To.ap.TypeCloud);
                //this.group.items[x].UploadID = "";//remuse
                group.items[x].Transfer = group.items[x].TransferRequest;//remuse
                group.items[x].ErrorMsg = "";//clear error
                group.items[x].Timestamp = CurrentMillis.Millis;
                switch (group.items[x].To.ap.TypeCloud)
                {
                    case CloudName.LocalDisk:
                        #region LocalDisk
                        ItemsTransfer.Add(new TransferBytes(group.items[x], null));
                        return;
                    #endregion

                    case CloudName.Dropbox:
                        #region Dropbox

                        int chunksizedb = 25;//default
                        int.TryParse(AppSetting.settings.GetSettingsAsString(SettingsKey.Dropbox_ChunksSize), out chunksizedb);
                        group.items[x].ChunkUploadSize = chunksizedb * 1024 * 1024;

                        DropboxRequestAPIv2 client = new DropboxRequestAPIv2(token);

                        if (string.IsNullOrEmpty(group.items[x].UploadID))//create upload id
                        {
                            group.items[x].byteread = group.items[x].From.stream.Read(group.items[x].buffer, 0, group.items[x].buffer.Length);
                            dynamic json = JsonConvert.DeserializeObject(client.upload_session_start(group.items[x].buffer, group.items[x].byteread));
                            group.items[x].UploadID = json.session_id;
                            group.items[x].Transfer += group.items[x].byteread;
                        }
                        ItemsTransfer.Add(new TransferBytes(group.items[x], client));
                        return;
                    #endregion

                    case CloudName.GoogleDrive:
                        #region GoogleDrive
                        DriveAPIHttprequestv2 gdclient = new DriveAPIHttprequestv2(JsonConvert.DeserializeObject<TokenGoogleDrive>(token));
                        gdclient.Email = group.items[x].To.ap.Email;
                        gdclient.TokenRenewEvent += GoogleDrive.Gdclient_TokenRenewEvent;

                        if (GoogleDrive.CreateFolder(group.items[x].To.ap.TypeCloud.ToString() + ":" + group.items[x].To.ap.Email + group.items[x].To.ap.Parent) != group.items[x].To.ap.Parent)
                            throw new Exception("Can't create folder: " + group.items[x].To.ap.TypeCloud.ToString() + ":" + group.items[x].To.ap.Email + group.items[x].To.ap.Parent);
                        
                        int chunksizeGD = 5;//default
                        int.TryParse(AppSetting.settings.GetSettingsAsString(SettingsKey.GD_ChunksSize), out chunksizeGD);
                        group.items[x].ChunkUploadSize = chunksizeGD * 1024 * 1024;

                        
                        if (string.IsNullOrEmpty(group.items[x].UploadID))//create upload id
                        {
                            string parentid = GoogleDrive.GetIdOfPath(group.items[x].To.ap.Parent, group.items[x].To.ap.Email);
                            string mimeType = Get_mimeType.Get_mimeType_From_FileExtension(group.items[x].To.ap.GetExtensionFile());
                            string jsondata = "{\"title\": \"" + group.items[x].From.filename + "\", \"mimeType\": \"" + mimeType + "\", \"parents\": [{\"id\": \"" + parentid + "\"}]}";
                            group.items[x].UploadID = gdclient.Files_insert_resumable_getUploadID(jsondata, mimeType, group.items[x].From.Size);
                        }
                        ItemsTransfer.Add(new TransferBytes(group.items[x], gdclient));
                        return;
                        #endregion
                }
            }
            catch (Exception ex) { group.items[x].ErrorMsg = ex.Message; group.items[x].status = StatusUpDown.Error; return; }
        }

        bool IsStillDownloading(int x)
        {
            return ((group.status == StatusUpDown.Running || group.status == StatusUpDown.Waiting || group.status == StatusUpDown.Started) && group.items[x].status == StatusUpDown.Running);
        }
    }
}