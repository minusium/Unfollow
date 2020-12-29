﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Java.Util;
using Android.App;
using Android.Views;
using Android.Content;
using AndroidX.AppCompat.App;
using AndroidX.Preference;
using Google.Android.Material.Dialog;
using Google.Android.Material.Snackbar;
using Google.Android.Material.BottomNavigation;
using Madamin.Unfollow.Fragments;
using Madamin.Unfollow.Instagram;

namespace Madamin.Unfollow
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true,
        Icon = "@mipmap/ic_launcher", RoundIcon = "@mipmap/ic_launcher_round",
        WindowSoftInputMode = SoftInput.AdjustResize)]
    public class MainActivity :
        FragmentHostBase,
        IInstagramHost,
        IDataContainer,
        IUpdateServerHost,
        IErrorHost,
        ISnackBarHost
    {
        public MainActivity() : base(
            Resource.Layout.activity_main,
            Resource.Id.main_appbar,
            Resource.Id.main_container)
        {
            Create += MainActivity_OnCreate;
            SaveState += MainActivity_OnSaveState;
            BackButtonVisibilityChange += MainActivity_OnBackButtonVisibilityChange;
        }

        protected override void AttachBaseContext(Context context)
        {
            var prefs = PreferenceManager.GetDefaultSharedPreferences(context);

            var config = context.Resources?.Configuration;

            Debug.Assert(config != null);

            var appTheme = prefs.GetString("theme", "adaptive");
            AppCompatDelegate.DefaultNightMode = appTheme switch
            {
                "adaptive" => AppCompatDelegate.ModeNightFollowSystem,
                "light" => AppCompatDelegate.ModeNightNo,
                "dark" => AppCompatDelegate.ModeNightYes,
                _ => AppCompatDelegate.DefaultNightMode
            };

            var appLang = prefs.GetString("lang", "sysdef");
            if (appLang == "sysdef" ||
                appLang == null)
            {
                config.SetLocale(Locale.Default);
            }
            else
            {
                var locale = new Locale(appLang);
                config.SetLocale(locale);
            }

            base.AttachBaseContext(context.CreateConfigurationContext(config));
        }

        private void MainActivity_OnCreate(object sender, OnActivityCreateEventArgs e)
        {
            Debug.Assert(DataDir != null);
            Debug.Assert(CacheDir != null);

            try
            {
                Accounts = new Accounts(
                    Path.Combine(DataDir.AbsolutePath, "accounts"),
                    CacheDir.AbsolutePath
                );
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }

            _navbar = FindViewById<BottomNavigationView>(Resource.Id.main_navbar);
            if (_navbar != null)
            {
                _navbar.NavigationItemSelected += Navbar_NavigationItemSelected;
            }

            Fragments.Add(new AccountsFragment());
            Fragments.Add(new SettingsFragment());

            if (e.SavedInstanceBundle != null)
            {
                // TODO: Load Accounts Data
                return;
            }

            var pref = PreferenceManager.GetDefaultSharedPreferences(this);
            if (pref.GetBoolean("auto_update_check", true))
            {
                CheckForUpdate(false);
            }
        }

        private void MainActivity_OnSaveState(object sender, OnSaveStateEventArgs e)
        {
            // TODO: Save Accounts Data
        }

        private void Navbar_NavigationItemSelected(object sender,
            BottomNavigationView.NavigationItemSelectedEventArgs e)
        {
            switch (e.Item.ItemId)
            {
                case Resource.Id.navbar_main_item_accounts:
                    NavigateTo(0);
                    break;

                case Resource.Id.navbar_main_item_settings:
                    NavigateTo(1);
                    break;
            }
        }

        private void MainActivity_OnBackButtonVisibilityChange(object sender, OnBackButtonVisibilityChangeEventArgs e)
        {
            _navbar.Visibility = e.Visible ? ViewStates.Gone : ViewStates.Visible;
        }

        public Accounts Accounts { get; private set; }

        public void OpenInInstagram(string username)
        {
            var intent = Intent.ParseUri(
                "https://instagram.com/_u/" + username,
                IntentUriType.None);
            intent?.SetPackage("com.instagram.android");
            try
            {
                StartActivity(intent);
            }
            catch (ActivityNotFoundException)
            {
                ShowSnackbar(Resource.String.error_ig_not_installed);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        public void SaveData(string fileName, object data)
        {
            Debug.Assert(DataDir != null);

            var filePath = Path.Combine(DataDir.AbsolutePath, fileName);
            using var file = new FileStream(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.Write);
            new BinaryFormatter().Serialize(file, data);
        }

        public object LoadData(string fileName)
        {
            Debug.Assert(DataDir != null);

            var filePath = Path.Combine(DataDir.AbsolutePath, fileName);
            using var file = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read);
            return new BinaryFormatter().Deserialize(file);
        }

        public bool DataExists(string fileName)
        {
            Debug.Assert(DataDir != null);

            return File.Exists(Path.Combine(DataDir.AbsolutePath, fileName));
        }

        public void CheckForUpdate(bool verbose)
        {
            RunOnUiThread(async () =>
            {
                try
                {
                    var package = PackageManager?.GetPackageInfo(PackageName, 0);
                    Debug.Assert(package != null);
                    var request = new CheckUpdateRequest
                    {
                        Version = (int) package.LongVersionCode
                    };
                    var result = await _updateServer.CheckUpdate(request);
                    if (result.Status == "ok")
                    {
                        if (result.Result.Update.Available)
                        {
                            var dialog = new MaterialAlertDialogBuilder(this);
                            dialog.SetTitle(Resource.String.title_update_available);
                            dialog.SetMessage(result.Result.Update.Message);
                            dialog.SetPositiveButton(
                                result.Result.Update.Label,
                                (sender, args) =>
                                {
                                    if (result.Result.Update.Url == "unfollow:ok")
                                        return;
                                    var intent = Intent.ParseUri(result.Result.Update.Url, IntentUriType.None);
                                    try
                                    {
                                        StartActivity(intent);
                                    }
                                    catch (Exception ex)
                                    {
                                        ShowError(ex);
                                    }
                                });
                            dialog.SetNegativeButton(
                                Android.Resource.String.Cancel,
                                (sender, args) => { });
                            dialog.Show();
                        }
                        else
                        {
                            if (verbose)
                            {
                                ShowSnackbar(Resource.String.msg_up_to_date);
                            }
                        }
                    }
                    else
                    {
                        throw new Exception(result.Error);
                    }
                }
                catch (Exception ex)
                {
                    if (verbose)
                    {
                        ShowError(ex);
                    }
                }
            });
        }

        public void ShowError(Exception exception)
        {
            var container = FindViewById(Resource.Id.main_container);
            var snack = Snackbar.Make(
                container, 
                Resource.String.msg_error, 
                Snackbar.LengthLong);
            snack.SetAnchorView(Resource.Id.main_navbar);
            snack.SetAction(Resource.String.button_text_details, view =>
            {
                var dialog = new MaterialAlertDialogBuilder(this);
                dialog.SetTitle(Resource.String.title_error);
                dialog.SetMessage(exception.ToString());
                dialog.SetPositiveButton(Resource.String.button_text_report, async (sender, args) =>
                {
                    try
                    {
                        ShowSnackbar(Resource.String.msg_sending_report);
                        await _updateServer.BugReport(
                            new BugReportRequest
                            {
                                Exception = new ExceptionData
                                {
                                    Type = exception.GetType().FullName,
                                    Message = exception.Message,
                                    CallStack = exception.StackTrace
                                }
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        ShowError(ex);
                    }
                });
                dialog.SetNegativeButton(
                    Android.Resource.String.Cancel,
                    (sender, args) => { });
                dialog.Show();
            });
            if (_navbar.Visibility == ViewStates.Visible)
                snack.SetAnchorView(_navbar);
            snack.Show();
        }

        public void ShowSnackbar(int res)
        {
            var rootView = FindViewById(Resource.Id.root);
            var snack = Snackbar.Make(rootView, res, Snackbar.LengthLong);
            //if (_navbar.Visibility == ViewStates.Visible)
            //snack.SetAnchorView(_navbar);
            snack.Show();
        }

        private BottomNavigationView _navbar;

        private readonly UpdateServerApi _updateServer = new UpdateServerApi(
            UpdateServerApi.UpdateServerHost,
            UpdateServerApi.UpdateServerUserAgent);
    }

    public interface IDataContainer
    {
        void SaveData(string fileName, object data);
        object LoadData(string fileName);
        bool DataExists(string fileName);
    }

    public interface IUpdateServerHost
    {
        void CheckForUpdate(bool verbose);
    }

    public interface ISnackBarHost
    {
        void ShowSnackbar(int res);
    }

    public interface IErrorHost
    {
        void ShowError(Exception ex);
    }
}