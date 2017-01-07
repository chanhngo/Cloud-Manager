﻿using SupDataDll;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfUI.UI
{
    /// <summary>
    /// Interaction logic for RenameItem.xaml
    /// </summary>
    public partial class RenameItem : Window
    {
        public RenameItem(string raw_path, string id, string oldname)
        {
            InitializeComponent();
            TB_oldname.Text = oldname;
            TB_newname.Text = oldname;
            this.raw_path = raw_path;
            this.id = id;
            this.oldname = oldname;
        }

        private string raw_path;
        private string id;
        private string oldname;

        private void BT_cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BT_change_Click(object sender, RoutedEventArgs e)
        {
            DoRename();
        }

        private void TB_newname_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter && BT_change.Visibility == Visibility.Visible) DoRename();
        }

        private void TB_newname_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TB_newname.Text == TB_oldname.Text) BT_change.Visibility = Visibility.Hidden;
            else BT_change.Visibility = Visibility.Visible;
        }
        void DoRename()
        {
            Thread thr = new Thread(Rename);
            Setting_UI.ManagerThreads.rename.Add(thr);
            Setting_UI.ManagerThreads.CleanThr();
            thr.Start();
        }

        void Rename()
        {
            AnalyzePath ap = new AnalyzePath(raw_path);
            if (ap.TypeCloud == CloudName.GoogleDrive ?
                        Setting_UI.reflection_eventtocore._MoveItem(null, null, id, null, null, TB_newname.Text, ap.Email, CloudName.GoogleDrive) :
                        Setting_UI.reflection_eventtocore._MoveItem(ap.AddRawChildPath(oldname), ap.AddRawChildPath(TB_newname.Text), id, null, null, null, null)
                )
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    MessageBox.Show(this, "Rename successful.", "Message response",MessageBoxButton.OK,MessageBoxImage.Information);
                    this.Close();
                }));

            }
            else
            {
                bool flag = false;
                Dispatcher.Invoke(new Action(() =>
                {
                    MessageBoxResult result = MessageBox.Show(this, "Rename Error\r\nPress Ok to retry.", "Message response", MessageBoxButton.OKCancel, MessageBoxImage.Error);
                    if (result == MessageBoxResult.OK) flag = true;
                    else this.Close();
                }));
                if (flag) Rename();
            }
        }

    }
}
