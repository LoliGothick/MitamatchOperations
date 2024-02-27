﻿using System.Diagnostics;
using System;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using mitama;
using JWT.Builder;
using Newtonsoft.Json;
using mitama.Domain;
using System.Threading.Channels;
using Mitama.Lib;
using Google.Cloud.BigQuery.V2;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MitamatchOperations
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private readonly Channel<Result<DiscordUser, string>> channel = Channel.CreateUnbounded<Result<DiscordUser, string>>();
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("##SyncfusionLicense##");
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Get the activation args
            var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

            // Get or register the main instance
            var mainInstance = AppInstance.FindOrRegisterForKey("main");

            // If the main instance isn't this current instance
            if (!mainInstance.IsCurrent)
            {
                // Redirect activation to that instance
                await mainInstance.RedirectActivationToAsync(appArgs);

                // And exit our instance and stop
                Process.GetCurrentProcess().Kill();
                return;
            }
            else
            {
                On_Activated(null, appArgs);
            }

            // Otherwise, register for activation redirection
            AppInstance.GetCurrent().Activated += On_Activated;

            var splash = new SplashScreen(typeof(MainWindow), async () =>
            {
                var item = await channel.Reader.ReadAsync();
                switch (item)
                {
                    case Ok<DiscordUser, string>(var user):
                        {
                            Upsert(user);
                            return true;
                        }
                    case Err<DiscordUser, string>:
                        {
                            return false;
                        }
                };
                return false;
            });
            splash.Completed += (_, e) => m_window = e;
        }

        private async void On_Activated(object _, AppActivationArguments args)
        {
            if (args.Kind == ExtendedActivationKind.Protocol)
            {
                var eventArgs = args.Data as Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs;
                var query = eventArgs.Uri.Query;
                var queryDictionary = System.Web.HttpUtility.ParseQueryString(query);
                var jwtToken = queryDictionary["token"];
                // verify JWT token
                try
                {
                    var json = JwtBuilder
                        .Create()
                        .WithAlgorithm(new JWT.Algorithms.HMACSHA256Algorithm())
                        .WithSecret("secret")
                        .MustVerifySignature()
                        .Decode(jwtToken);
                    // JSON から MyToken オブジェクトへのデシリアライズ
                    var user = JsonConvert.DeserializeObject<DiscordUser>(json);
                    await channel.Writer.WriteAsync(new Ok<DiscordUser, string>(user));
                }
                catch (Exception ex)
                {
                    await channel.Writer.WriteAsync(new Err<DiscordUser, string>(ex.Message));
                }
            }
        }

        private void Upsert(DiscordUser user)
        {
            var cred = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(@"##GOOGLE_CLOUD_CREDENCIALS##");

            BigQueryClient client = BigQueryClient.Create("asaultlily", cred);
            var dataset = client.GetDataset("mitamatch");
            var table = dataset.GetTable("users");

            var rows = new BigQueryInsertRow[]
            {
                new() {
                    { "id", user.id },
                    { "name", user.global_name },
                    { "email", user.email },
                    { "avatar", user.avatar },
                    { "updated_at", DateTime.Now },
                }
            };
            table.InsertRows(rows);
        }

        private Window m_window;
    }
}
