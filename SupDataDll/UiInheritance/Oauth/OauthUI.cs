﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SupDataDll.UiInheritance.Oauth
{
    public interface OauthUI
    {
        string Url { set; }
        string CheckUrl { set; }
        void ShowUI(object owner);
    }
}
