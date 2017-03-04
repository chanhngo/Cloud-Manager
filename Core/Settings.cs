﻿using Core.StaticClass;
using SupDataDll;
using SupDataDll.Crypt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Core
{
    public class Settings
    {
        private XmlDocument xmlSettings;
        private object SyncSetting = new object();
        public object SyncPass = new object();
        public void ReadSettings()
        {
            xmlSettings = new XmlDocument();
            if (ReadWriteData.Exists(ReadWriteData.File_Settings))
            {
                lock (SyncSetting)
                {
                    xmlSettings.Load(ReadWriteData.Read(ReadWriteData.File_Settings));
                }
            }
            else
            {
                xmlSettings.LoadXml(global::Core.Properties.Resources.SettingDefault);
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            lock (SyncSetting)
            {
                MemoryStream Stream = new MemoryStream();
                TextWriter TxtWriter = new StreamWriter(Stream, Encoding.UTF8);
                xmlSettings.Save(TxtWriter);
                ReadWriteData.Write(ReadWriteData.File_Settings, Stream.GetBuffer());
            }
        }

        public string GetSettingsAsString(SettingsKey Key)
        {
            return GetSettingsAsString_FromString(Key.ToString());
        }

        public string GetSettingsAsString_FromString(string Key)
        {
            XmlNodeList SettingsList = xmlSettings.DocumentElement.SelectSingleNode("SETTINGS").ChildNodes;
            string returnValue = string.Empty;
            foreach (XmlNode Setting in SettingsList)
            {
                if (Setting.Attributes["NAME"].Value != Key) continue;
                returnValue = Setting.Attributes["VALUE"].Value;
                break;
            }
            return returnValue;
        }

        public void SetSettingAsString(SettingsKey Key, string Data)
        {
            SetSettingAsString_FromString(Key.ToString(), Data);
        }

        public void SetSettingAsString_FromString(string Key, string Data)
        {
            XmlNode SettingsNode = xmlSettings.DocumentElement.SelectSingleNode("SETTINGS");
            foreach (XmlNode Setting in SettingsNode.ChildNodes)
            {
                if (Setting.Attributes["NAME"].Value != Key) continue;
                Setting.Attributes["VALUE"].Value = Data;
                return;
            }
            XmlNode NewSetting = xmlSettings.CreateElement("KEY");
            XmlAttribute Attrib = xmlSettings.CreateAttribute("NAME");
            Attrib.Value = Key;
            NewSetting.Attributes.Append(Attrib);
            Attrib = xmlSettings.CreateAttribute("VALUE");
            Attrib.Value = Data;
            NewSetting.Attributes.Append(Attrib);
            SettingsNode.AppendChild(NewSetting);
        }

        public bool ChangeUserPass(string user, string pass, string newpass)
        {
            bool flag = false;
            if (Md5.CreateMD5String(pass) != GetSettingsAsString(SettingsKey.Admin_password)) return flag;
            else
            {
                if (!string.IsNullOrEmpty(user))
                {
                    SetSettingAsString(SettingsKey.Admin_user, user);
                    flag = true;
                }

                if (!string.IsNullOrEmpty(newpass))
                {
                    foreach (XmlNode node in GetCloudDataList())
                    {
                        string token = GetToken(node.Attributes["Email"].Value, (CloudType)Enum.Parse(typeof(CloudType), node.Attributes["CloudName"].Value));
                        if (!string.IsNullOrEmpty(token)) ChangeToken(node, token, newpass);
                    }
                    SetSettingAsString(SettingsKey.Admin_password,Md5.CreateMD5String(newpass));
                    SaveSettings();
                    AppSetting.Pass = newpass;
                    flag = true;
                }
                return flag;
            }

        }



        #region Cloud
        public List<CloudEmail_Type> GetListAccountCloud()
        {
            List<CloudEmail_Type> list = new List<CloudEmail_Type>();
            foreach (XmlNode node in GetCloudDataList())
            {
                list.Add(new CloudEmail_Type()
                {
                    Email = node.Attributes["Email"].Value,
                    Type = (CloudType)Enum.Parse(typeof(CloudType), node.Attributes["CloudName"].Value)
                });
            }
            return list;
        }

        public XmlNodeList GetCloudDataList()
        {
            return xmlSettings.DocumentElement.SelectSingleNode("UserAccount").ChildNodes;
        }

        internal XmlNode GetCloud(string Email, CloudType cloudname)
        {
            foreach (XmlNode node in GetCloudDataList())
            {
                if (node.Attributes["Email"].Value == Email & node.Attributes["CloudName"].Value == cloudname.ToString())
                    return node;
            }
            return null;
        }

        internal string GetToken(string Email, CloudType cloudname)
        {
            XmlNode node = GetCloud(Email, cloudname);
            if (node == null) return null;
            string token;
            try
            {
                Monitor.Enter(SyncPass);
                token = Crypto.DecryptStringAES(node.Attributes["Token"].Value, AppSetting.Pass);
            }
            finally { Monitor.Exit(SyncPass); }
#if DEBUG
            Console.WriteLine("Token: " + Email + ":" + cloudname.ToString() + " : " + token);
#endif
            return token;

        }

        internal string GetRootID(string Email, CloudType cloudname)
        {
            XmlNode node = GetCloud(Email, cloudname);
            if (node == null) return null;
#if DEBUG
            try
            {
                return node.Attributes["RootID"].Value;
            }
            catch
            {
                CreateNewAttribute(node, "RootID", "");
                return null;
            }
#else
            return node.Attributes["RootID"].Value;
#endif
        }

        public string GetDefaultCloud(CloudType cloudname)
        {
            foreach (XmlNode node in GetCloudDataList())
            {
                if (node.Attributes["CloudName"].Value == cloudname.ToString() && node.Attributes["Default"].Value == "1") return node.Attributes["Email"].Value;
            }
            foreach (XmlNode node in GetCloudDataList())
            {
                if (node.Attributes["CloudName"].Value == cloudname.ToString() && node.Attributes["Default"].Value == "0")
                {
                    node.Attributes["Default"].Value = "1";
                    SaveSettings();
                    return node.Attributes["Email"].Value;
                }
            }
            return null;
        }



        void CreateNewAttribute(XmlNode Node, string AttributeName, string Value)
        {
            XmlAttribute Attrib = xmlSettings.CreateAttribute(AttributeName);
            Attrib.Value = Value;
            Node.Attributes.Append(Attrib);
        }


        public bool AddCloud(string Email, CloudType cloudname, string token, bool AreEncrypt, bool IsDefault = false, string root_id = null)
        {
            if (string.IsNullOrEmpty(AppSetting.Pass)) throw new Exception("Pass is null.");
            if (GetToken(Email, cloudname) == null)
            {
                XmlNode NewSetting = xmlSettings.CreateElement("Cloud");

                CreateNewAttribute(NewSetting, "Email", Email);
                CreateNewAttribute(NewSetting, "CloudName", cloudname.ToString());
                if (!AreEncrypt) token = Crypto.EncryptStringAES(token, AppSetting.Pass);
                CreateNewAttribute(NewSetting, "Token", token);
                CreateNewAttribute(NewSetting, "Default", "0");
                CreateNewAttribute(NewSetting, "RootID", root_id == null ? "" : root_id);
                xmlSettings.DocumentElement.SelectSingleNode("UserAccount").AppendChild(NewSetting);
                if (IsDefault) SetDefaultCloud(Email, cloudname);
                SaveSettings();
                return true;
            }
            return false;
        }

        public bool SetDefaultCloud(string Email, CloudType cloudname)
        {
            XmlNode n = GetCloud(Email, cloudname);
            if (n != null)
            {
                foreach (XmlNode node in GetCloudDataList())
                {
                    if (node.Attributes["CloudName"].Value == cloudname.ToString() && node.Attributes["Default"].Value == "1") node.Attributes["Default"].Value = "0";
                }
                n.Attributes["Default"].Value = "1";
                return true;
            }
            return false;
        }

        public bool RemoveCloud(string Email, CloudType cloudname)
        {
            XmlNode cloud = GetCloud(Email, cloudname);
            if (cloud != null)
            {
                xmlSettings.DocumentElement.SelectSingleNode("UserAccount").RemoveChild(cloud);
                SaveSettings();
                return true;
            }
            return false;
        }

        internal void ChangeToken(XmlNode cloud, string newtoken, string newpass = null)
        {
            if (cloud != null)
            {
                try
                {
                    Monitor.Enter(AppSetting.settings.SyncPass);
                    cloud.Attributes["Token"].Value = Crypto.EncryptStringAES(newtoken, newpass == null ? AppSetting.Pass : newpass);
                    AppSetting.settings.SaveSettings();
                    return;
                }
                finally { Monitor.Exit(AppSetting.settings.SyncPass); }
            }
            throw new Exception("Can't save token");
        }

        internal void SetRootID(string Email, CloudType cloudname, string RootID)
        {
            if (string.IsNullOrEmpty(RootID)) return;
            XmlNode node = GetCloud(Email, cloudname);
            if (node == null) throw new Exception("Cloud not found");
#if DEBUG
            try
            {
                node.Attributes["RootID"].Value = RootID;
            }
            catch
            {
                CreateNewAttribute(node, "RootID", RootID);
            }
#else
            node.Attributes["RootID"].Value = RootID;
#endif
        }
        #endregion
    }
}
