﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SupDataDll.UiInheritance;
using SupDataDll;

namespace WPF.UI
{
    /// <summary>
    /// Interaction logic for UILogin.xaml
    /// </summary>
    public partial class UILogin : Window, SupDataDll.UiInheritance.UILogin
    {
        public string user = "";
        public string pass = "";
        public bool autologin = false;
        public UILogin()
        {
            InitializeComponent();
        }

        #region interface
        public bool ShowInTaskbar_
        {
            get
            {
                return this.ShowInTaskbar;
            }

            set
            {
                this.ShowInTaskbar = value;
            }
        }
        
        public SupDataDll.UiInheritance.WindowState WindowState_
        {
            get
            {
                return (SupDataDll.UiInheritance.WindowState)(int)this.WindowState;
            }

            set
            {
                this.WindowState = (System.Windows.WindowState)(int)value;
            }
        }

        public void Load_User(string User, string Pass, bool AutoLogin)
        {
            this.user = User;
            this.pass = Pass;
            this.autologin = AutoLogin;
        }

        public void ShowDialog_()
        {
            this.ShowDialog();
        }
        #endregion

        public void Check(string user, string pass, bool autologin)
        {
            if (!Setting_UI.reflection_eventtocore._Login(user, pass, autologin))
            {
                MessageBox.Show("Login failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (this.WindowState ==  System.Windows.WindowState.Minimized) this.WindowState = System.Windows.WindowState.Normal;
                if (this.ShowInTaskbar == false) this.ShowInTaskbar = true;
            }
            else this.Close();
        }
        public void LoadLanguage()
        {
            this.Title = Setting_UI.reflection_eventtocore._GetTextLanguage(LanguageKey.Form_Text);
            LB_user.Content = Setting_UI.reflection_eventtocore._GetTextLanguage(LanguageKey.LB_User);
            LB_pass.Content = Setting_UI.reflection_eventtocore._GetTextLanguage(LanguageKey.LB_pass);
            BT_login.Content = Setting_UI.reflection_eventtocore._GetTextLanguage(LanguageKey.BT_Login);
            BT_cancel.Content = Setting_UI.reflection_eventtocore._GetTextLanguage(LanguageKey.BT_cancel);
            CB_autologin.Content = Setting_UI.reflection_eventtocore._GetTextLanguage(LanguageKey.CB_autologin);
        }

        #region Event UI
        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            TB_user.Text = this.user;
            PB_pass.Password = this.pass;
            CB_autologin.IsChecked = this.autologin;
            if (autologin == true & CB_autologin.IsChecked != null)
            {
                Check(TB_user.Text, PB_pass.Password, CB_autologin.IsChecked.Value);
            }
            LoadLanguage();
        }
        private void BT_login_Click(object sender, RoutedEventArgs e)
        {
            Check(TB_user.Text, PB_pass.Password, CB_autologin.IsChecked.Value);
        }

        private void BT_cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void TB_pass_KeyDown(object sender, KeyEventArgs e)
        {
            Check(TB_user.Text, PB_pass.Password, CB_autologin.IsChecked.Value);
        }
        #endregion

    }
}