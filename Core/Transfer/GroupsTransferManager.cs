﻿using Core.StaticClass;
using Newtonsoft.Json;
using SupDataDll;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;

namespace Core.Transfer
{
    public class GroupsTransferManager
    {
        public Thread MainThread;
        public StatusUpDownApp status = StatusUpDownApp.Pause; //UploadDownloadItems
        public List<ItemsTransferManager> groups = new List<ItemsTransferManager>();
        public List<Thread> threads = new List<Thread>();
        public bool AuToStartGroupMode = true;
        public bool Loop = true;
        long timestamp;
        public void Start()
        {
            MainThread = new Thread(LoadMainThread);
            MainThread.Start();
        }

        private void LoadMainThread()
        {
            int count_group_running = 0;
            int count = 0;
            bool flag_shutdown = false;
            timestamp = CurrentMillis.Millis;
            bool lockkillthr = false;
            ReadData();
            while (Loop)
            {
                GC.Collect();
                Thread.Sleep(100);
                if (status == StatusUpDownApp.Pause) continue;
                for (int i = 0; i < threads.Count; i++)
                {
                    if (!threads[i].IsAlive)
                    {
                        threads.RemoveAt(i);
                        i--;
                    }
                }
                switch (status)//UploadDownloadItems
                {
                    case StatusUpDownApp.Start:
                        #region Start
                        //count
                        count_group_running = 0;
                        count = 0;
                        groups.ForEach(s =>
                        {
                            if (s.group.status == StatusUpDown.Running) count_group_running++;
                            else if (s.group.status == StatusUpDown.Started | s.group.status == StatusUpDown.Waiting | s.group.status == StatusUpDown.Loading) count++;
                        });

                        for (int i = 0; i < groups.Count; i++)
                        {
                            if (AuToStartGroupMode && groups[i].group.status == StatusUpDown.Waiting && count_group_running < int.Parse(AppSetting.settings.GetSettingsAsString(SettingsKey.MaxGroupsDownload)))//auto
                            {
                                int.TryParse(AppSetting.settings.GetSettingsAsString(SettingsKey.MaxItemsInGroupDownload), out groups[i].group.MaxItemsDownload);
                                groups[i].group.status = StatusUpDown.Started;
                                count_group_running++;
                            }

                            if (groups[i].group.status == StatusUpDown.Removing & groups[i].threads.Count == 0)
                            {
                                AppSetting.uc_lv_ud_instance.RemoveGroup(this.groups[i].group);
                                this.groups.RemoveAt(i);
                                i--;
                                continue;
                            }

                            groups[i].ManagerItemsAndRefreshData();
                        }

                        if (!flag_shutdown && count_group_running > 0 && AppSetting.settings.GetSettingsAsString(SettingsKey.ShutdownWhenDone) == "1") flag_shutdown = true;
                        if (flag_shutdown && AppSetting.settings.GetSettingsAsString(SettingsKey.ShutdownWhenDone) == "0") flag_shutdown = false;
                        if (flag_shutdown && AppSetting.settings.GetSettingsAsString(SettingsKey.ShutdownWhenDone) == "1" && (count_group_running + count) == 0)
                        {
                            SaveData();
                            Console.WriteLine("shutdown");
                            //Process.Start("shutdown.exe", "-s -t 30");
                            return;
                        }

                        if (CurrentMillis.Millis - timestamp > 500)
                        {
                            timestamp = CurrentMillis.Millis;
                            AppSetting.uc_lv_ud_instance.RefreshAll();
                        }

                        SaveData();
                        #endregion
                        break;
                    case StatusUpDownApp.StopForClosingApp:
                        #region Stop
                        Eventupdateclosingform(AppSetting.lang.GetText(LanguageKey.CloseThread.ToString()));
                        int thrcount = 0;
                        foreach (ItemsTransferManager group in groups)
                        {
                            if (group.group.status == StatusUpDown.Running | group.group.status == StatusUpDown.Started |
                                group.group.status == StatusUpDown.Waiting) group.group.status = StatusUpDown.Stop;
                            if (lockkillthr) KillThreads(group.threads);
                            group.ManagerItemsAndRefreshData();//clean thread
                            thrcount += group.threads.Count;
                        }
                        thrcount += this.threads.Count;
                        //if (lockkillthr) KillThreads(threads);
                        if (thrcount == 0) this.status = StatusUpDownApp.SavingData;
                        else if (CurrentMillis.Millis - timestamp > 5000 & !lockkillthr) lockkillthr = true;
                        #endregion
                        break;
                    case StatusUpDownApp.SavingData:
                        #region SavingData
                        Eventupdateclosingform(AppSetting.lang.GetText(LanguageKey.SaveData.ToString()));
                        //Thread.Sleep(200);
                        SaveData();
                        Eventcloseapp();
                        #endregion
                        return;
                    default: break;
                }
            }
        }

        public void AddItems(List<UpDownloadItem> items, string fromfolder_raw, string savefolder_raw, bool AreCut)
        {
            ItemsTransferManager gr = new ItemsTransferManager(items, fromfolder_raw, savefolder_raw, AreCut);
            groups.Add(gr);
            Thread thr = new Thread(Loaditem);
            thr.Start(groups.IndexOf(gr));
            threads.Add(thr);
        }
        public void Loaditem(object obj)
        {
            groups[(int)obj].LoadListItems();
        }

        public void LoadGroupToListView()
        {
            foreach (ItemsTransferManager gr in groups)
            {
                AppSetting.uc_lv_ud_instance.AddNewGroup(gr.group);
            }
        }

        void KillThreads(List<Thread> thrs)
        {
            for (int i = 0; i < thrs.Count; i++)
            {
                if (thrs[i] != null && thrs[i].IsAlive) KillThread(thrs[i]);
            }
        }
        [SecurityPermissionAttribute(SecurityAction.Demand, ControlThread = true)]
        void KillThread(Thread thr)
        {
            thr.Abort();
        }

        #region Closing Form
        public void Exit()
        {
            timestamp = CurrentMillis.Millis;
        }
        public delegate void updateclosingform(string text);
        public event updateclosingform Eventupdateclosingform;
        public delegate void closeapp();
        public event closeapp Eventcloseapp;
        #endregion

        #region Save/Read Data When Close/Open program
        public void ReadData()
        {
            if (ReadWriteData.Exists(ReadWriteData.File_DataUploadDownload))
            {
                List<JsonDataSaveGroup> json_groups = JsonConvert.DeserializeObject<List<JsonDataSaveGroup>>(ReadWriteData.Read(ReadWriteData.File_DataUploadDownload).ReadToEnd());
                foreach (JsonDataSaveGroup json_group in json_groups)
                {
                    ItemsTransferManager group = new ItemsTransferManager(json_group);
                    this.groups.Add(group);
                }
            }
        }

        string temp_jsonSaveData = "";
        public void SaveData()
        {
            if (this.groups.Count == 0) return;
            List<JsonDataSaveGroup> json_groups = new List<JsonDataSaveGroup>();
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].group.status == StatusUpDown.Loading) continue;
                JsonDataSaveGroup json_item = new JsonDataSaveGroup();
                json_item.fromfolder_raw = groups[i].fromfolder.Path_Raw;
                json_item.savefolder_raw = groups[i].savefolder.Path_Raw;
                json_item.Group = groups[i].group;
                json_item.AreCut = groups[i].AreCut;
                json_groups.Add(json_item);
            }
            string json = JsonConvert.SerializeObject(json_groups);
            if (temp_jsonSaveData == json) return;
            else temp_jsonSaveData = json;
            ReadWriteData.Write(ReadWriteData.File_DataUploadDownload, Encoding.UTF8.GetBytes(json));
        }
        #endregion
    }

}
