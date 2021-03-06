﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CloudManagerGeneralLib
{
    public enum CloudType
    {
        LocalDisk = 0,
        Folder = 1,
        Dropbox = 2,
        GoogleDrive = 3,
        Mega = 4,
        MediaFire = 5,
        OneDrive = 6
    }
    public enum DiskType
    {
        Unknown = 0,
        NoRootDirectory = 1,
        Removable = 2,
        Fixed = 3,
        Network = 4,
        CDRom = 5,
        Ram = 6,
        Cloud = 7
    }

    public enum StatusUpDownApp
    {
        Pause,
        Start,
        StopForClosingApp,
        SavingData
    }

    public enum StatusTransfer
    {
        Loading,//AddItem -> loading info (before Started)

        //set
        Started,
        Waiting,//waiting for running
        Remove,//remove

        //get
        Running,//transfer
        Done,//complete
        Error,//error transfer
        //Removing,

        //get set
        Stop,//stop
        
        Moved,
        
        Added
    }

    public enum ChangeTLV
    {
        ProcessingToDone,
        Done,

        DoneToProcessing,
        Processing
    }

    public enum SettingsKey
    {
        Admin_user, Admin_password, AutoLogin,
        UI_dll_file, lang,
        DATE_TIME_FORMAT,
        HOST_STREAM, HOST_STREAM_PORT,

        mimeType_audio, mimeType_document, mimeType_drawing, mimeType_form,
        mimeType_fusiontable, mimeType_map, mimeType_photo, mimeType_presentation,
        mimeType_script, mimeType_sites, mimeType_spreadsheet, mimeType_unknown, mimeType_video,

        MaxGroupsDownload,MaxItemsInGroupDownload,
        BufferSize,GD_ChunksSize,Dropbox_ChunksSize,
        AutoStartTransfer,ShutdownWhenDone
    }
    public enum LanguageKey
    {
        //button
        BT_cancel, BT_save, BT_close, BT_create, BT_yes,

        ReloadLang,
        //longin form
        Form_Text,
        LB_User, LB_pass, BT_Login,
        CB_autologin,
        //mainform
        ToolStrip_files, ToolStrip_cloud, ToolStrip_settings, //menu
        ToolStrip_files_exit, ToolStrip_cloud_add, ToolStrip_cloud_remove, ToolStrip_settings_setting,//child ^
        //UC_lv_ud
        TP_processing, TP_done,// tabpage
        TSMI_ChangeStatus, TSMI_numberOfParallelDownloads, TSMI_remove, TSMI_start, TSMI_stop, TSMI_waiting, TSMI_error, TSMI_forcestart, TSMI_forcewaiting,//menu
        //Uc_lv_item
        newtab, addtab, removetab,//newtab
        TB_path,
        TSMI_refresh, TSMI_open, TSMI_cut, TSMI_copy, TSMI_paste, TSMI_rename, TSMI_delete, TSMI_createfolder, TSMI_copyid, TSMI_downloadsellected, TSMI_uploadfolder, TSMI_uploadfile,//menu
        //closeform
        CloseThread, SaveData,
        //ChangeNumberItemDownload form
        ChangeNumberItemDownload,
        //CreateFolderForm
        CreateFolderForm_name,
        //DeleteConfirmForm
        DeleteConfirmForm_text, DeleteConfirmForm_waning, CB_pernament,
        //DeleteForm
        DeleteForm_text, DeleteForm_CB_autoclose, DeleteForm_updatetext_Deleting, DeleteForm_updatetext_Deleted, DeleteForm_updatetext_Error,
    }

    public enum UpdateTransfer_TLVUD
    {
        Add,
        Remove,
        Refresh
    }
}
