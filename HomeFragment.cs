﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using AndroidX.Fragment.App;
using Google.Android.Material.AppBar;
using Google.Android.Material.Button;
using Google.Android.Material.Dialog;
using Google.Android.Material.TextView;

namespace madamin.unfollow
{
    public class HomeFragment : Fragment
    {
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return inflater.Inflate(Resource.Layout.fragment_home, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            var ig = (IInstagramActivity)Activity;

            var appbar = view.FindViewById<MaterialToolbar>(Resource.Id.home_appbar);
            appbar.MenuItemClick += async (sender, args) =>
            {
                switch (args.Item.ItemId)
                {
                    case Resource.Id.appmenu_item_refresh:
                        try
                        {
                            await ig.RefreshCache();
                        }
                        catch
                        {
                            Toast.MakeText(Activity, Resource.String.error, ToastLength.Long).Show();
                        }
                        ((INavigationHost)Activity).NavigateTo(new HomeFragment(), false);
                        break;
                    case Resource.Id.appmenu_item_about:
                        new MaterialAlertDialogBuilder(Activity)
                        .SetTitle(Resource.String.menu_about)
                        .SetNeutralButton(Android.Resource.String.Ok, (dialog, Activity) => { })
                        .SetMessage(Resource.String.msg_about)
                        .Show();
                        break;
                    case Resource.Id.appmenu_item_exit:
                        Activity.Finish();
                        break;
                }
            };

            view.FindViewById<MaterialTextView>(Resource.Id.home_fullname).Text =
                ig.Instagram.Data.User.Fullname;

            view.FindViewById<MaterialTextView>(Resource.Id.home_username).Text =
                "@" + ig.Instagram.Data.User.Username;

            view.FindViewById<MaterialTextView>(Resource.Id.home_ffnumbers).Text =
                string.Format(GetString(Resource.String.msg_ffnumber),
                ig.Instagram.Data.Followings.Count, ig.Instagram.Data.Followers.Count);

            var logout = view.FindViewById<MaterialButton>(Resource.Id.home_button_logout);
            logout.Click += (button, args) =>
            {
                logout.Enabled = false;
                try
                {
                    ig.Logout();
                }
                catch
                {
                    logout.Enabled = true;
                    Toast.MakeText(Activity, Resource.String.error, ToastLength.Long);
                }
            };
        }
    }
}